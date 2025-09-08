namespace FunSharp.DeviantArt.Server

open System
open System.Threading
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.RequestErrors
open Suave.Writers
open FunSharp.Common
open FunSharp.DeviantArt.Api
open FunSharp.Data
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Server.Helpers

module ServerStartup =

    let secrets = Secrets.load ()
    let authPersistence = Persistence.AuthenticationPersistence()
    let dataPersistence = new PickledPersistence(@"C:\Files\FunSharp.DeviantArt\persistence.db") :> IPersistence
    let apiClient = Client(authPersistence, secrets.client_id, secrets.client_secret)
        
    let galleryId galleryName =
        secrets.galleries |> Array.find(fun x -> x.name = galleryName) |> _.id
        
    let galleryName galleryId =
        secrets.galleries |> Array.find(fun x -> x.id = galleryId) |> _.name
        
    let withGalleryName publishedDeviation =
        { publishedDeviation with PublishedDeviation.Metadata.Gallery = galleryName publishedDeviation.Metadata.Gallery }
        
    let withGalleryId stashedDeviation =
        { stashedDeviation with StashedDeviation.Metadata.Gallery = galleryId stashedDeviation.Metadata.Gallery }

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
        fun ctx ->
            {| Galleries = secrets.galleries |}
            |> asOkJsonResponse <| ctx
        
    let downloadInspirations: WebPart =
        fun ctx -> async {
            return! dataPersistence.FindAll<Inspiration>(dbKey_Inspirations)
                    |> asOkJsonResponse <| ctx
        }
        
    let downloadPrompts: WebPart =
        fun ctx -> async {
            return! dataPersistence.FindAll<Prompt>(dbKey_Prompts)
                    |> asOkJsonResponse <| ctx
        }
        
    let downloadLocalDeviations: WebPart =
        fun ctx -> async {
            return! dataPersistence.FindAll<LocalDeviation>(dbKey_LocalDeviations)
                    |> asOkJsonResponse <| ctx
        }
        
    let downloadStashedDeviations: WebPart =
        fun ctx -> async {
            return! dataPersistence.FindAll<StashedDeviation>(dbKey_StashedDeviations)
                    |> asOkJsonResponse <| ctx
        }
        
    let downloadPublishedDeviations: WebPart =
        fun ctx -> async {
            return! dataPersistence.FindAll<PublishedDeviation>(dbKey_PublishedDeviations)
                    |> asOkJsonResponse <| ctx
        }
            
    // match ctx.request.queryParam "id" with
    // | Choice1Of2 id -> 
    //     return! Successful.OK (sprintf "Got id = %s" id) ctxOpt
    // | Choice2Of2 msg -> 
    //     return! RequestErrors.BAD_REQUEST (sprintf "Missing param: %s" msg) ctxOpt
        
    let downloadImage: WebPart =
        fun ctx -> async {
            match ctx.request.queryParam "id" with
            | Choice2Of2 _ ->
                return! BAD_REQUEST "No files uploaded" ctx
            | Choice1Of2 id ->
                return! dataPersistence.Find<string, Image>(dbKey_Images, id) |> asOkJsonResponse <| ctx
        }
        
    let uploadLocalDeviations: WebPart =
        fun ctx -> async {
            match ctx.request.files with
            | [] -> return! BAD_REQUEST "No files uploaded" ctx
            | files ->
                try
                    let mutable items = []
                    
                    for file in files do
                        let key = file.fileName
                        let! content = file.tempFilePath |> File.readAllBytesAsync
                        let deviation = { LocalDeviation.empty with Id = key }
                        let image = Image(key, file.mimeType, content)
                        
                        do dataPersistence.Insert(dbKey_LocalDeviations, key, deviation)
                        do dataPersistence.Insert(dbKey_Images, key, image)
                        
                        items <- items @ [(deviation, image)]

                    return! items |> asOkJsonResponse <| ctx
                with ex ->
                    return! BAD_REQUEST ex.Message ctx
        }
        
    let updateLocalDeviation: WebPart =
        fun ctx -> async {
            try
                let deviation = ctx.request |> asJson<LocalDeviation>
                let key = deviation.Id
                
                match dataPersistence.Find<string, LocalDeviation>(dbKey_LocalDeviations, key) with
                | None -> return! BAD_REQUEST $"local deviation '{key}' not found" ctx
                | Some _ ->
                    printfn $"Updating '{key}'..."
                    
                    do dataPersistence.Update(dbKey_LocalDeviations, key, deviation) |> ignore
                    
                    printfn "Update done!"
                    
                    return! "ok" |> asOkJsonResponse <| ctx
                
            with ex ->
                return! BAD_REQUEST ex.Message ctx
        }
        
    let stash: WebPart =
        fun ctx -> async {
            try
                let key = ctx.request |> asString
                
                let deviation = dataPersistence.Find<string, LocalDeviation>(dbKey_LocalDeviations, key)
                let image = dataPersistence.Find<string, Image>(dbKey_Images, key)
                
                match deviation, image with
                | None, _ -> return! BAD_REQUEST $"Local deviation '{key}' not found" ctx
                | _, None -> return! BAD_REQUEST $"Image for local deviation '{key}' not found" ctx
                | Some local, Some image ->
                    printfn $"Submitting '{key}' to stash..."
                    
                    let! stashedDeviation = submitToStash apiClient local image
                    
                    do dataPersistence.Delete(dbKey_LocalDeviations, key) |> ignore
                    do dataPersistence.Insert(dbKey_StashedDeviations, key, stashedDeviation)
                    
                    printfn "Submission done!"
                    
                    return! stashedDeviation |> asOkJsonResponse <| ctx
                
            with ex ->
                return! BAD_REQUEST ex.Message ctx
        }
        
    let publish: WebPart =
        fun ctx -> async {
            try
                let key = ctx.request |> asString
                
                match dataPersistence.Find<string, StashedDeviation>(dbKey_StashedDeviations, key) with
                | None -> return! BAD_REQUEST $"stashed deviation '{key}' not found" ctx
                | Some stashedDeviation ->
                    printfn $"Publishing '{key}' from stash..."
                    
                    let stashedDeviation = stashedDeviation |> withGalleryId
                    
                    let! publishedDeviation = publishFromStash apiClient stashedDeviation
                    
                    do dataPersistence.Delete(dbKey_StashedDeviations, key) |> ignore
                    do dataPersistence.Insert(dbKey_PublishedDeviations, key, publishedDeviation)
                    
                    printfn "Publishing done!"
                    
                    return! publishedDeviation |> withGalleryName |> asOkJsonResponse <| ctx
                
            with ex ->
                return! BAD_REQUEST ex.Message ctx
        }

    let cts = new CancellationTokenSource()
    
    let serverConfiguration = {
        defaultConfig with
            cancellationToken = cts.Token
            bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 5123 ] 
    }
        
    let serverApp =
        
        let apiBase = "/api/v1"
        
        allowCors >=> choose [
            corsPreflight
            
            GET >=> path $"{apiBase}/user/name" >=> username
            GET >=> path $"{apiBase}/settings" >=> settings
            
            GET >=> path $"{apiBase}/local/inspirations" >=> downloadInspirations
            GET >=> path $"{apiBase}/local/prompts" >=> downloadPrompts
            GET >=> path $"{apiBase}/local/deviations" >=> downloadLocalDeviations
            GET >=> path $"{apiBase}/stash" >=> downloadStashedDeviations
            GET >=> path $"{apiBase}/publish" >=> downloadPublishedDeviations
            GET >=> path $"{apiBase}/image" >=> downloadImage
            
            POST >=> path $"{apiBase}/local/inspiration" >=> BAD_REQUEST "not implemented yet"
            POST >=> path $"{apiBase}/local/prompt" >=> BAD_REQUEST "not implemented yet"
            POST >=> path $"{apiBase}/local/deviation" >=> BAD_REQUEST "not implemented yet"
            POST >=> path $"{apiBase}/stash" >=> stash
            POST >=> path $"{apiBase}/publish" >=> publish
            
            POST >=> path $"{apiBase}/local/deviation/asImages" >=> uploadLocalDeviations
            
            PATCH >=> path $"{apiBase}/local/deviation" >=> updateLocalDeviation
            
            NOT_FOUND "Unknown route"
        ]
        
    let tryStartServer () =
        try
            let who = apiClient.WhoAmI() |> AsyncResult.getOrFail |> Async.RunSynchronously
            printfn $"Hello, {who.username}!"
            
            startWebServer serverConfiguration serverApp
        with
        | :? System.Net.Sockets.SocketException as ex ->
            printfn $"Socket bind failed: %s{ex.Message}"
            
    [<EntryPoint>]
    let main _ =
        Async.Start(async { do tryStartServer () }, cancellationToken = cts.Token)
        
        printfn "Press Enter to stop..."
        Console.ReadLine() |> ignore

        printfn "Shutting down..."
        
        cts.Cancel()
        dataPersistence.Dispose()
        
        0
