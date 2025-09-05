namespace FunSharp.DeviantArt.Server

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
    let client = Client(authPersistence, secrets.client_id, secrets.client_secret)

    let allowCors: WebPart =
        setHeader "Access-Control-Allow-Origin" "*"
        >=> setHeader "Access-Control-Allow-Headers" "Content-Type"
        >=> setHeader "Access-Control-Allow-Methods" "GET, POST, PUT, PATCH, DELETE, OPTIONS"

    let corsPreflight: WebPart =
        pathRegex ".*" >=> OPTIONS >=> allowCors >=> OK "CORS preflight"

    let username: WebPart =
        fun ctx -> async {
            let! username =
                client.WhoAmI()
                |> AsyncResult.getOrFail
                |> Async.map _.username
                
            return! {| username = username |} |> asOkJsonResponse <| ctx
        }

    let settings: WebPart =
        fun ctx -> {| Galleries = secrets.galleries |} |> asOkJsonResponse <| ctx
        
    let downloadLocalDeviations: WebPart =
        fun ctx -> dataPersistence.FindAll<LocalDeviation>(dbKey_LocalDeviations) |> asOkJsonResponse <| ctx
            
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
                    
                    printfn $"saving deviation: {deviation}"
                    
                    dataPersistence.Insert(dbKey_LocalDeviations, key, deviation)

                    return! "ok" |> asOkJsonResponse <| ctx
                with ex ->
                    return! BAD_REQUEST ex.Message ctx

            | _ -> return! BAD_REQUEST "Upload of multiple files is currently not supported" ctx
        }
        
    let updateLocalDeviation: WebPart =
        fun ctx ->
            try
                let deviation = ctx.request |> asJson<LocalDeviation>
                
                dataPersistence.Upsert(dbKey_LocalDeviations, deviation.Image.Name, deviation) |> ignore
                
                "ok" |> asOkJsonResponse <| ctx
                
            with ex ->
                BAD_REQUEST ex.Message ctx

    // let stash: WebPart =
    //     fun ctx -> async {
    //         match ctx.request.files with
    //         | [] -> return! BAD_REQUEST "No files uploaded" ctx
    //
    //         | [ file ] ->
    //             let title =
    //                 match ctx.request.fieldData "title" with
    //                 | Choice1Of2 v -> v
    //                 | Choice2Of2 v -> failwith $"{v}"
    //
    //             let! response = submitToStash client title file
    //
    //             return! response |> asOkJsonResponse <| ctx
    //
    //         | _ -> return! BAD_REQUEST "Multiple files not supported" ctx
    //     }

    [<EntryPoint>]
    let main _ =

        let config =
            { defaultConfig with
                bindings = [ HttpBinding.createSimple HTTP "0.0.0.0" 5123 ] }

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
            POST >=> path $"{apiBase}/stash" >=> BAD_REQUEST "not implemented yet"
            POST >=> path $"{apiBase}/publish" >=> BAD_REQUEST "not implemented yet"

            PATCH >=> path $"{apiBase}/local/deviation" >=> updateLocalDeviation
            
            NOT_FOUND "Unknown route"
        ]
        |> startWebServer config

        0
