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

    let getSettings (secrets: Secrets) : WebPart =
        
        fun ctx ->
            {| Galleries = secrets.galleries; Snippets = secrets.snippets |}
            |> asOkJsonResponse <| ctx
        
    let getInspirations (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            return! dataPersistence.FindAll<Inspiration>(dbKey_Inspirations)
                    |> asOkJsonResponse <| ctx
        }
        
    let getPrompts (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            return! dataPersistence.FindAll<Prompt>(dbKey_Prompts)
                    |> asOkJsonResponse <| ctx
        }
        
    let getLocalDeviations (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            return! dataPersistence.FindAll<LocalDeviation>(dbKey_LocalDeviations)
                    |> asOkJsonResponse <| ctx
        }
        
    let getStashedDeviations (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            return! dataPersistence.FindAll<StashedDeviation>(dbKey_StashedDeviations)
                    |> asOkJsonResponse <| ctx
        }
        
    let getPublishedDeviations (dataPersistence: IPersistence) : WebPart =
        
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
        
    let addInspiration (serverAddress: string) (serverPort: int) (dataPersistence: IPersistence) (apiClient: Client) : WebPart =
        
        fun ctx -> async {
            try
                let url = ctx.request |> asString |> HttpUtility.HtmlDecode
                
                match inspirationUrlAlreadyExists dataPersistence url with
                | true ->
                    return! badRequestMessage ctx "addInspiration()" "This inspiration url already has a published deviation."
                    
                | false ->
                    let! id = apiClient.GetDeviationId url |> AsyncResult.getOrFail
                    let! deviation = apiClient.GetDeviation id |> AsyncResult.getOrFail
                    
                    let fileName = $"{id}.jpg"
                    
                    let! imageContent = Http.downloadFile deviation.preview.src
                    do! File.writeAllBytesAsync $"{imagesLocation}/{fileName}" imageContent
                    
                    let imageUrl = imageUrl serverAddress serverPort fileName
                    
                    let inspiration = {
                        Url = Uri url
                        Timestamp = DateTimeOffset.Now
                        ImageUrl = Some imageUrl
                    }
                    
                    do dataPersistence.Insert(dbKey_Inspirations, url, inspiration)
                    
                    return! inspiration |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "addInspiration()" ex
        }
        
    let uploadLocalDeviations (serverAddress: string) (serverPort: int) (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            match ctx.request.files with
            | [] ->
                return! badRequestMessage ctx "uploadLocalDeviations()" "No files uploaded"
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
        
    let updateLocalInDatabase (ctx: HttpContext) (dataPersistence: IPersistence) (key: string) (deviation: LocalDeviation) =
        printfn $"Updating '{key}'..."
                        
        do dataPersistence.Update(dbKey_LocalDeviations, key, deviation) |> ignore
        
        printfn "Update done!"
        
        deviation |> asOkJsonResponse <| ctx
        
    let updateLocalDeviation (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            try
                let deviation = ctx.request |> asJson<LocalDeviation>
                let key = deviation.ImageUrl.ToString()
                
                match dataPersistence.Find<string, LocalDeviation>(dbKey_LocalDeviations, key) with
                | None ->
                    return! badRequestMessage ctx "updateLocalDeviation()" $"local deviation '{key}' not found"
                | Some _ ->
                    match deviation.Origin with
                    | DeviationOrigin.Inspiration inspiration ->
                        match inspiration.Url.ToString() |> inspirationUrlAlreadyExists dataPersistence with
                        | true ->
                            return! badRequestMessage ctx "addInspiration()" "This inspiration url already has a published deviation."
                        | false ->
                            return! updateLocalInDatabase ctx dataPersistence key deviation
                    | _ ->
                        return! updateLocalInDatabase ctx dataPersistence key deviation
                
            with ex ->
                return! badRequestException ctx "updateLocalDeviation()" ex
        }
        
    let inspiration2Prompt (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            try
                let payload = ctx.request |> asJson<Inspiration2Prompt>
                
                let inspiration = dataPersistence.Find(dbKey_Inspirations, payload.Inspiration.ToString()) |> Option.get
                
                let prompt: Prompt = {
                    Id = Guid.NewGuid()
                    Timestamp = DateTimeOffset.Now
                    Inspiration = Some inspiration
                    Text = payload.Text
                }
                
                do dataPersistence.Delete(dbKey_Inspirations, inspiration.Url.ToString()) |> ignore
                do dataPersistence.Insert(dbKey_Prompts, prompt.Id.ToString(), prompt)
                
                return! prompt |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "inspiration2Prompt()" ex
        }
        
    let prompt2Deviation (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            try
                let payload = ctx.request |> asJson<Prompt2LocalDeviation>
                
                let prompt = dataPersistence.Find(dbKey_Prompts, payload.Prompt.ToString()) |> Option.get
                
                let deviation : LocalDeviation = {
                    ImageUrl = payload.ImageUrl
                    Timestamp = DateTimeOffset.Now
                    Metadata = Metadata.empty
                    Origin = DeviationOrigin.Prompt prompt
                }
                
                do dataPersistence.Delete(dbKey_Prompts, prompt.Id.ToString()) |> ignore
                do dataPersistence.Insert(dbKey_LocalDeviations, deviation.ImageUrl.ToString(), deviation)
                
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

    let forgetInspiration (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            try
                match ctx.request.queryParam "url" with
                | Choice2Of2 _ ->
                    return! badRequestMessage ctx "forgetInspiration()" "could not identify inspiration"
                | Choice1Of2 url ->
                    let url = url |> HttpUtility.UrlDecode
                    printfn $"forgetInspiration(): {url}"
                    
                    do dataPersistence.Delete(dbKey_Inspirations, url) |> ignore
                    
                    return! "ok" |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "forgetInspiration()" ex
        }

    let forgetPrompt (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            try
                match ctx.request.queryParam "id" with
                | Choice2Of2 _ ->
                    return! badRequestMessage ctx "forgetPrompt()" "could not identify prompt"
                | Choice1Of2 key ->
                    do dataPersistence.Delete(dbKey_Prompts, key) |> ignore
                    
                    return! "ok" |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "forgetPrompt()" ex
        }

    let forgetLocalDeviation (dataPersistence: IPersistence) : WebPart =
        
        fun ctx -> async {
            try
                match ctx.request.queryParam "url" with
                | Choice2Of2 _ ->
                    return! badRequestMessage ctx "forgetLocalDeviation()" "could not identify deviation"
                | Choice1Of2 url ->
                    let url = url |> HttpUtility.UrlDecode
                    printfn $"forgetLocalDeviation(): {url}"
                    
                    do dataPersistence.Delete(dbKey_LocalDeviations, url) |> ignore
                    
                    return! "ok" |> asOkJsonResponse <| ctx
                
            with ex ->
                return! badRequestException ctx "forgetLocalDeviation()" ex
        }
