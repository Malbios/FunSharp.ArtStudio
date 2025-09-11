namespace FunSharp.DeviantArt.Server

open System
open System.Web
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Writers
open FunSharp.Common
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Server.Helpers

module WebParts =
        
    let allowCors : WebPart =
        
        setHeader "Access-Control-Allow-Origin" "*"
        >=> setHeader "Access-Control-Allow-Headers" "Content-Type"
        >=> setHeader "Access-Control-Allow-Methods" "GET, POST, PUT, PATCH, DELETE, OPTIONS"

    let corsPreflight : WebPart =
        
        pathRegex ".*" >=> OPTIONS >=> allowCors >=> OK "CORS preflight"

    let username (apiClient: Client) : WebPart =
        
        fun ctx -> async {
            let! username =
                apiClient.WhoAmI()
                |> AsyncResult.getOrFail
                |> Async.map _.username
                
            return! {| username = username |} |> asOkJsonResponse <| ctx
        }

    let settings (secrets: Secrets) : WebPart =
        
        fun ctx ->
            {| Galleries = secrets.galleries |}
            |> asOkJsonResponse <| ctx
        
    let downloadInspirations (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            return! dataPersistence.FindAll<Inspiration>(dbKey_Inspirations)
                    |> asOkJsonResponse <| ctx
        }
        
    let downloadPrompts (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            return! dataPersistence.FindAll<Prompt>(dbKey_Prompts)
                    |> asOkJsonResponse <| ctx
        }
        
    let downloadLocalDeviations (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            return! dataPersistence.FindAll<LocalDeviation>(dbKey_LocalDeviations)
                    |> asOkJsonResponse <| ctx
        }
        
    let downloadStashedDeviations (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            return! dataPersistence.FindAll<StashedDeviation>(dbKey_StashedDeviations)
                    |> asOkJsonResponse <| ctx
        }
        
    let downloadPublishedDeviations (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            return! dataPersistence.FindAll<PublishedDeviation>(dbKey_PublishedDeviations)
                    |> asOkJsonResponse <| ctx
        }
        
    let uploadImages (serverAddress: string) (serverPort: int) : WebPart =
        
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
        
    let uploadLocalDeviations (serverAddress: string) (serverPort: int) (dataPersistence: IPersistence) : WebPart =
        
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
        
    let updateLocalDeviation (dataPersistence: IPersistence) : WebPart =
        
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
                    
                    return! deviation |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "updateLocalDeviation()" ex
        }
        
    let addInspiration (serverAddress: string) (serverPort: int) (dataPersistence: IPersistence) (apiClient: Client) : WebPart =
        
        fun ctx -> async {
            try
                let url = ctx.request |> asString |> HttpUtility.HtmlDecode
                
                let! id = apiClient.GetDeviationId url |> AsyncResult.getOrFail
                let! deviation = apiClient.GetDeviation id |> AsyncResult.getOrFail
                
                let fileName = $"{id}.jpg"
                
                let! imageContent = Http.downloadFile deviation.preview.src
                do! File.writeAllBytesAsync $"{imagesLocation}/{fileName}" imageContent
                
                let imageUrl = imageUrl serverAddress serverPort fileName
                
                let inspiration = {
                    Url = Uri url
                    ImageUrl = Some imageUrl
                }
                
                do dataPersistence.Insert(dbKey_Inspirations, url, inspiration)
                
                return! inspiration |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "addInspiration()" ex
        }
        
    let inspiration2Prompt (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            try
                let payload = ctx.request |> asJson<Inspiration2Prompt>
                
                let inspiration = dataPersistence.Find(dbKey_Inspirations, payload.Inspiration) |> Option.get
                
                let prompt: Prompt = {
                    Id = Guid.NewGuid()
                    Inspiration = Some inspiration
                    Text = payload.Text
                }
                
                do dataPersistence.Delete(dbKey_Inspirations, inspiration.Url) |> ignore
                do dataPersistence.Insert(dbKey_Prompts, prompt.Id, prompt)
                
                return! prompt |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "inspiration2Prompt()" ex
        }
        
    let prompt2Deviation (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            try
                let payload = ctx.request |> asJson<Prompt2LocalDeviation>
                
                let prompt = dataPersistence.Find(dbKey_Prompts, payload.Prompt) |> Option.get
                
                let deviation : LocalDeviation = {
                    ImageUrl = payload.ImageUrl
                    Metadata = Metadata.empty
                    Origin = DeviationOrigin.Prompt prompt
                }
                
                do dataPersistence.Delete(dbKey_Prompts, prompt.Id) |> ignore
                do dataPersistence.Insert(dbKey_LocalDeviations, deviation.ImageUrl, deviation)
                
                return! deviation |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "inspiration2Prompt()" ex
        }
        
    let stash (dataPersistence: IPersistence) (apiClient: Client) : WebPart =
        
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
        
    let publish (secrets: Secrets) (dataPersistence: IPersistence) (apiClient: Client) : WebPart =
        
        fun ctx -> async {
            try
                let key = ctx.request |> asString
                
                match dataPersistence.Find<string, StashedDeviation>(dbKey_StashedDeviations, key) with
                | None -> return! badRequestMessage ctx "publish()" $"stashed deviation '{key}' not found"
                | Some stashedDeviation ->
                    printfn $"Publishing '{key}' from stash..."
                    
                    let galleryId = galleryId secrets stashedDeviation.Metadata.Gallery
                    
                    let! publishedDeviation = publishFromStash apiClient galleryId stashedDeviation
                    
                    do dataPersistence.Delete(dbKey_StashedDeviations, key) |> ignore
                    do dataPersistence.Insert(dbKey_PublishedDeviations, key, publishedDeviation)
                    
                    printfn "Publishing done!"
                    
                    return! publishedDeviation |> asOkJsonResponse <| ctx
                    
            with ex ->
                return! badRequestException ctx "publish()" ex
        }
