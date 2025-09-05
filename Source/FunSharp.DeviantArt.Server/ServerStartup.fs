namespace FunSharp.DeviantArt.Server

open System
open System.Threading
open FunSharp.Data
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Server.Helpers
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.RequestErrors
open Suave.Writers
open FunSharp.Common
open FunSharp.DeviantArt.Api

module ServerStartup =

    let secrets = Secrets.load ()
    let authPersistence = Persistence.AuthenticationPersistence()
    let dataPersistence : Abstraction.IPersistence = PickledPersistence("persistence.db")
    let apiClient = Client(authPersistence, secrets.client_id, secrets.client_secret)

    let allowCors: WebPart =
        setHeader "Access-Control-Allow-Origin" "*"
        >=> setHeader "Access-Control-Allow-Headers" "Content-Type"
        >=> setHeader "Access-Control-Allow-Methods" "GET, POST, PUT, PATCH, DELETE, OPTIONS"

    let corsPreflight: WebPart =
        pathRegex ".*" >=> OPTIONS >=> allowCors >=> OK "CORS preflight"

    let username: WebPart =
        fun ctx -> async {
            let! username =
                apiClient.WhoAmI()
                |> AsyncResult.getOrFail
                |> Async.map _.username
                
            return! {| username = username |} |> asOkJsonResponse <| ctx
        }

    let settings: WebPart =
        fun ctx -> {| Galleries = secrets.galleries |} |> asOkJsonResponse <| ctx
        
    let downloadLocalDeviations: WebPart =
        fun ctx -> async {
            return! dataPersistence.FindAll<LocalDeviation>(dbKey_LocalDeviations) |> asOkJsonResponse <| ctx
        }
        
    let uploadLocalDeviation: WebPart =
        fun ctx -> async {
            match ctx.request.files with
            | [] -> return! BAD_REQUEST "No files uploaded" ctx

            | [ file ] ->
                try
                    let key = file.fileName
                    let! content = file.tempFilePath |> File.readAllBytesAsync
                    
                    let deviation = {
                        LocalDeviation.empty with
                            Image = Image(key, file.mimeType, content)
                    }
                    
                    do dataPersistence.Insert(dbKey_LocalDeviations, key, deviation)

                    return! "ok" |> asOkJsonResponse <| ctx
                with ex ->
                    return! BAD_REQUEST ex.Message ctx

            | _ -> return! BAD_REQUEST "Upload of multiple files is currently not supported" ctx
        }
        
    let updateLocalDeviation: WebPart =
        fun ctx -> async {
            try
                let deviation = ctx.request |> asJson<LocalDeviation>
                let key = keyOf deviation
                
                do dataPersistence.Upsert(dbKey_LocalDeviations, key, deviation) |> ignore
                
                return! "ok" |> asOkJsonResponse <| ctx
                
            with ex ->
                return! BAD_REQUEST ex.Message ctx
        }

    let stash: WebPart =
        fun ctx -> async {
            try
                let key = ctx.request |> asString
                
                match dataPersistence.Find<string, LocalDeviation>(dbKey_LocalDeviations, key) with
                | None -> return! BAD_REQUEST $"local deviation '{key}' not found" ctx
                | Some localDeviation ->
                    let! stashedDeviation = submitToStash apiClient localDeviation
                    
                    do dataPersistence.Delete(dbKey_LocalDeviations, key) |> ignore
                    do dataPersistence.Insert(dbKey_StashedDeviations, key, stashedDeviation)
                    
                    return! "ok" |> asOkJsonResponse <| ctx
                
            with ex ->
                return! BAD_REQUEST ex.Message ctx
        }
        
        // fun ctx -> async {
        //     match ctx.request.files with
        //     | [] -> return! BAD_REQUEST "No files uploaded" ctx
        //
        //     | [ file ] ->
        //         let title =
        //             match ctx.request.fieldData "title" with
        //             | Choice1Of2 v -> v
        //             | Choice2Of2 v -> failwith $"{v}"
        //
        //         let! response = submitToStash client title file
        //
        //         return! response |> asOkJsonResponse <| ctx
        //
        //     | _ -> return! BAD_REQUEST "Multiple files not supported" ctx
        // }

    let cts = new CancellationTokenSource()
    
    let startServer () =
        
        let config = {
            defaultConfig with
                cancellationToken = cts.Token
                bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 5123 ] 
        }
        
        let apiBase = "/api/v1"

        allowCors >=> choose [
            corsPreflight

            GET >=> path $"{apiBase}/user/name" >=> username
            GET >=> path $"{apiBase}/settings" >=> settings

            GET >=> path $"{apiBase}/local/inspirations" >=> BAD_REQUEST "not implemented yet"
            GET >=> path $"{apiBase}/local/prompts" >=> BAD_REQUEST "not implemented yet"
            GET >=> path $"{apiBase}/local/deviations" >=> downloadLocalDeviations
            GET >=> path $"{apiBase}/stash" >=> BAD_REQUEST "not implemented yet"
            GET >=> path $"{apiBase}/publish" >=> BAD_REQUEST "not implemented yet"

            POST >=> path $"{apiBase}/local/inspiration" >=> BAD_REQUEST "not implemented yet"
            POST >=> path $"{apiBase}/local/prompt" >=> BAD_REQUEST "not implemented yet"
            POST >=> path $"{apiBase}/local/deviation" >=> uploadLocalDeviation
            POST >=> path $"{apiBase}/stash" >=> stash
            POST >=> path $"{apiBase}/publish" >=> BAD_REQUEST "not implemented yet"

            PATCH >=> path $"{apiBase}/local/deviation" >=> updateLocalDeviation
            
            NOT_FOUND "Unknown route"
        ]
        |> startWebServer config
    
    [<EntryPoint>]
    let main _ =
        Async.Start(async { do startServer () }, cancellationToken = cts.Token)
        
        printfn "Press Enter to stop..."
        Console.ReadLine() |> ignore

        printfn "Shutting down..."
        
        cts.Cancel()
        dataPersistence.Dispose()
        
        0
