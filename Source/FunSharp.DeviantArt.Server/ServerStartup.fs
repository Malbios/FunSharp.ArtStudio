namespace FunSharp.DeviantArt.Server

open System
open System.Threading
open System.Web
open Suave
open Suave.Files
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
    
    let serverAddress = "127.0.0.1"
    let serverPort = 5123
    
    let secrets = Secrets.load ()
    let authPersistence = Persistence.AuthenticationPersistence()
    let dataPersistence = new PickledPersistence(@"C:\Files\FunSharp.DeviantArt\persistence.db") :> IPersistence
    let apiClient = Client(authPersistence, secrets.client_id, secrets.client_secret)
        
    let galleryId galleryName =
        secrets.galleries |> Array.find(fun x -> x.name = galleryName) |> _.id
        
    let imageUrl fileName =
        Uri $"http://{serverAddress}:{serverPort}/images/{fileName}"
        
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
        
    let uploadImages: WebPart =
        fun ctx -> async {
            match ctx.request.files with
            | [] -> return! badRequestMessage ctx "uploadImages()" "No files uploaded"
            | files ->
                try
                    let mutable items = []
                    
                    for file in files do
                        let key = file.fileName
                        
                        let! content = File.readAllBytesAsync file.tempFilePath
                        do! File.writeAllBytesAsync $"{imagesLocation}\\{key}" content
                        
                        let imageUrl = Uri $"http://{serverAddress}:{serverPort}/images/{key}"
                        
                        items <- items @ [imageUrl]

                    return! items |> asOkJsonResponse <| ctx
                with ex ->
                    return! badRequestException ctx "uploadImages()" ex
        }
        
    let uploadLocalDeviations: WebPart =
        fun ctx -> async {
            match ctx.request.files with
            | [] -> return! badRequestMessage ctx "uploadLocalDeviations()" "No files uploaded"
            | files ->
                try
                    let mutable items = []
                    
                    for file in files do
                        let! content = File.readAllBytesAsync file.tempFilePath
                        do! File.writeAllBytesAsync $"{imagesLocation}\\{file.fileName}" content

                        let imageUrl = Uri $"http://{serverAddress}:{serverPort}/images/{file.fileName}"
                        
                        let deviation = LocalDeviation.defaults imageUrl
                        
                        do dataPersistence.Insert(dbKey_LocalDeviations, imageUrl.ToString(), deviation)
                        
                        items <- items @ [deviation]

                    return! items |> asOkJsonResponse <| ctx
                with ex ->
                    return! badRequestException ctx "uploadLocalDeviations()" ex
        }
        
    let updateLocalDeviation: WebPart =
        fun ctx -> async {
            try
                let deviation = ctx.request |> asJson<LocalDeviation>
                let key = deviation.ImageUrl.ToString()
                
                match dataPersistence.Find<string, LocalDeviation>(dbKey_LocalDeviations, key) with
                | None -> return! badRequestMessage ctx "updateLocalDeviation()" $"local deviation '{key}' not found"
                | Some _ ->
                    printfn $"Updating '{key}'..."
                    
                    do dataPersistence.Update(dbKey_LocalDeviations, key, deviation) |> ignore
                    
                    printfn "Update done!"
                    
                    return! "ok" |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "updateLocalDeviation()" ex
        }
        
    let addInspiration: WebPart =
        fun ctx -> async {
            try
                let url = ctx.request |> asString |> HttpUtility.HtmlDecode
                
                let! id = apiClient.GetDeviationId url |> AsyncResult.getOrFail
                let! deviation = apiClient.GetDeviation id |> AsyncResult.getOrFail
                
                let fileName = $"{id}.jpg"
                
                let! imageContent = Http.downloadFile deviation.preview.src
                do! File.writeAllBytesAsync $"{imagesLocation}/{fileName}" imageContent
                
                let imageUrl = imageUrl fileName
                
                let inspiration = {
                    Url = Uri url
                    ImageUrl = Some imageUrl
                }
                
                do dataPersistence.Insert(dbKey_Inspirations, url, inspiration)
                
                return! inspiration |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "addInspiration()" ex
        }
        
    let inspiration2Prompt: WebPart =
        fun ctx -> async {
            try
                let url = ctx.request |> asString |> HttpUtility.HtmlDecode
                
                let! id = apiClient.GetDeviationId url |> AsyncResult.getOrFail
                let! deviation = apiClient.GetDeviation id |> AsyncResult.getOrFail
                
                let fileName = $"{id}.jpg"
                
                let! imageContent = Http.downloadFile deviation.preview.src
                do! File.writeAllBytesAsync $"{imagesLocation}/{fileName}" imageContent
                
                let imageUrl = imageUrl fileName
                
                let inspiration = {
                    Url = Uri url
                    ImageUrl = Some imageUrl
                }
                
                do dataPersistence.Insert(dbKey_Inspirations, url, inspiration)
                
                return! inspiration |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "addInspiration()" ex
        }
        
    let prompt2Deviation: WebPart =
        fun ctx -> async {
            try
                let url = ctx.request |> asString |> HttpUtility.HtmlDecode
                
                let! id = apiClient.GetDeviationId url |> AsyncResult.getOrFail
                let! deviation = apiClient.GetDeviation id |> AsyncResult.getOrFail
                
                let fileName = $"{id}.jpg"
                
                let! imageContent = Http.downloadFile deviation.preview.src
                do! File.writeAllBytesAsync $"{imagesLocation}/{fileName}" imageContent
                
                let imageUrl = imageUrl fileName
                
                let inspiration = {
                    Url = Uri url
                    ImageUrl = Some imageUrl
                }
                
                do dataPersistence.Insert(dbKey_Inspirations, url, inspiration)
                
                return! inspiration |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "addInspiration()" ex
        }
        
    let stash: WebPart =
        fun ctx -> async {
            try
                let key = ctx.request |> asString
                
                let deviation = dataPersistence.Find<string, LocalDeviation>(dbKey_LocalDeviations, key)
                
                match deviation with
                | None -> return! badRequestMessage ctx "stash()" $"Local deviation '{key}' not found"
                | Some local ->
                    let fileName = Uri key |> FunSharp.Common.Uri.lastSegment |> HttpUtility.UrlDecode
                    let imagePath = $"{imagesLocation}\\{fileName}"
                    let mimeType = Helpers.mimeType imagePath
                    let! imageContent = File.readAllBytesAsync imagePath
                    
                    printfn $"Submitting '{key}' to stash..."
                    
                    let! stashedDeviation = submitToStash apiClient imageContent mimeType local
                    
                    do dataPersistence.Delete(dbKey_LocalDeviations, key) |> ignore
                    do dataPersistence.Insert(dbKey_StashedDeviations, key, stashedDeviation)
                    
                    printfn "Submission done!"
                    
                    return! stashedDeviation |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "stash()" ex
        }
        
    let publish: WebPart =
        fun ctx -> async {
            try
                let key = ctx.request |> asString
                
                match dataPersistence.Find<string, StashedDeviation>(dbKey_StashedDeviations, key) with
                | None -> return! badRequestMessage ctx "publish()" $"stashed deviation '{key}' not found"
                | Some stashedDeviation ->
                    printfn $"Publishing '{key}' from stash..."
                    
                    let galleryId = galleryId stashedDeviation.Metadata.Gallery
                    
                    let! publishedDeviation = publishFromStash apiClient galleryId stashedDeviation
                    
                    do dataPersistence.Delete(dbKey_StashedDeviations, key) |> ignore
                    do dataPersistence.Insert(dbKey_PublishedDeviations, key, publishedDeviation)
                    
                    printfn "Publishing done!"
                    
                    return! publishedDeviation |> asOkJsonResponse <| ctx
                    
            with ex ->
                return! badRequestException ctx "publish()" ex
        }
        
    let cts = new CancellationTokenSource()
    
    let serverConfiguration = {
        defaultConfig with
            cancellationToken = cts.Token
            bindings = [ HttpBinding.createSimple HTTP serverAddress serverPort ] 
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
            
            POST >=> path $"{apiBase}/local/inspiration" >=> addInspiration
            POST >=> path $"{apiBase}/local/prompt" >=> inspiration2Prompt
            POST >=> path $"{apiBase}/local/deviation" >=> prompt2Deviation
            POST >=> path $"{apiBase}/stash" >=> stash
            POST >=> path $"{apiBase}/publish" >=> publish
            
            POST >=> path $"{apiBase}/local/deviation/asImages" >=> uploadLocalDeviations
            
            PATCH >=> path $"{apiBase}/local/deviation" >=> updateLocalDeviation
            
            pathScan "/images/%s" (fun filename ->
                let filepath = System.IO.Path.Combine(imagesLocation, filename)
                file filepath
            )
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
