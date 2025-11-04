namespace FunSharp.ArtStudio.Server

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
open FunSharp.ArtStudio.Model
open FunSharp.ArtStudio.Client.Model
open FunSharp.ArtStudio.Server.Helpers

module WebParts =
    
    let allowCors =
        
        setHeader "Access-Control-Allow-Origin" "*"
        >=> setHeader "Access-Control-Allow-Headers" "Content-Type"
        >=> setHeader "Access-Control-Allow-Methods" "GET, POST, PUT, PATCH, DELETE, OPTIONS"

    let corsPreflight =
        
        pathRegex ".*" >=> OPTIONS >=> allowCors >=> OK "CORS preflight"

    let rec getUsername (apiClient: Client) =
        
        fun ctx ->
            apiClient.WhoAmI()
            |> Async.bind (fun response -> {| username = response.username |} |> asOkJsonResponse ctx)
        |> tryCatch (nameof getUsername)

    let rec getSettings (secrets: Secrets) =
        
        fun ctx ->
            {| Galleries = secrets.galleries; Snippets = secrets.snippets |}
            |> asOkJsonResponse ctx
        |> tryCatch (nameof getSettings)
        
    let rec getInspirations (persistence: IPersistence) =
        
        fun ctx ->
            persistence.FindAll<Inspiration>(dbKey_Inspirations)
            |> asOkJsonResponse ctx
        |> tryCatch (nameof getInspirations)
        
    let rec getPrompts (persistence: IPersistence) =
        
        fun ctx ->
            persistence.FindAll<Prompt>(dbKey_Prompts)
            |> asOkJsonResponse ctx
        |> tryCatch (nameof getPrompts)
        
    let private sortLocalDeviations (options: SortOptions) (items: LocalDeviation array) =
        
        match options.Property, options.Direction with
        | "timestamp", SortDirection.Ascending -> items |> Array.sortBy _.Timestamp
        | "timestamp", SortDirection.Descending -> items |> Array.sortByDescending _.Timestamp
        | property, _ -> failwith $"unsupported sort property: '{property}'"
        
    let rec getLocalDeviations (persistence: IPersistence) =
        
        fun ctx ->
            persistence.FindAll<LocalDeviation>(dbKey_LocalDeviations)
            |> withPagination ctx sortLocalDeviations
        |> tryCatch (nameof getLocalDeviations)
        
    let rec getStashedDeviations (persistence: IPersistence) =
        
        fun ctx ->
            persistence.FindAll<StashedDeviation>(dbKey_StashedDeviations)
            |> asOkJsonResponse ctx
        |> tryCatch (nameof getStashedDeviations)
        
    let rec getPublishedDeviations (persistence: IPersistence) =
        
        fun ctx ->
            persistence.FindAll<PublishedDeviation>(dbKey_PublishedDeviations)
            |> asOkJsonResponse ctx
        |> tryCatch (nameof getPublishedDeviations)
        
    let rec putImages (serverAddress: string) (serverPort: int) =
        
        fun ctx -> async {
            match ctx.request.files with
            | [] ->
                return! badRequestMessage ctx (nameof putImages) "No files uploaded"
            
            | files ->
                let mutable items = []
                
                for file in files do
                    let key = file.fileName
                    
                    let! content = File.readAllBytesAsync file.tempFilePath
                    do! File.writeAllBytesAsync $"{imagesLocation}\\{key}" content
                    
                    let imageUrl = Uri $"http://{serverAddress}:{serverPort}/images/{key}"
                    
                    items <- items @ [imageUrl]
                    
                return! items |> asOkJsonResponse ctx
        }
        |> tryCatch (nameof putImages)
        
    let processNewInspiration (serverAddress: string) (serverPort: int) (persistence: IPersistence) (apiClient: Client) (url: Uri) = async {
        let! id = apiClient.GetDeviationId url |> Async.getOrFail
        let! deviation = apiClient.GetDeviation id |> Async.getOrFail
        
        let fileName = $"{id}.jpg"
        
        let! imageContent = apiClient.DownloadFile(deviation.preview.src)
        do! File.writeAllBytesAsync $"{imagesLocation}/{fileName}" imageContent
        
        let imageUrl = imageUrl serverAddress serverPort fileName
        
        let inspiration = {
            Url = url
            Timestamp = DateTimeOffset.Now
            ImageUrl = Some imageUrl
        }
        
        persistence.Insert(dbKey_Inspirations, url, inspiration)
        
        return inspiration
    }
        
    let rec putInspiration (serverAddress: string) (serverPort: int) (persistence: IPersistence) (apiClient: Client) =
        
        fun ctx ->
            let url = ctx.request |> asString |> HttpUtility.HtmlDecode |> Uri
            
            match urlAlreadyExists persistence url with
            | true ->
                badRequestMessage ctx (nameof putInspiration) "This inspiration url already has a published deviation."
                
            | false ->
                processNewInspiration serverAddress serverPort persistence apiClient url
                |> asOkJsonResponse ctx
        |> tryCatch (nameof putInspiration)
        
    let rec patchPrompt (persistence: IPersistence) =
        
        fun ctx ->
            let prompt = ctx.request |> asJson<Prompt>
            let existingPrompt = persistence.Find<Guid, Prompt>(dbKey_Prompts, Prompt.keyOf prompt)
            
            let isDuplicate = promptHasDuplicateInspiration persistence prompt existingPrompt
            
            if isDuplicate then
                badRequestMessage ctx (nameof patchPrompt) "This new inspiration url already has another deviation."
            else
                upsertPrompt ctx persistence prompt
        |> tryCatch (nameof patchPrompt)
        
    let rec patchLocalDeviation (persistence: IPersistence) =
        
        fun ctx ->
            let deviation = ctx.request |> asJson<LocalDeviation>
            let existingDeviation = persistence.Find<Uri, LocalDeviation>(dbKey_LocalDeviations, LocalDeviation.keyOf deviation)
            
            let isDuplicate = localDeviationHasDuplicateInspiration persistence deviation existingDeviation
            
            if isDuplicate then
                badRequestMessage ctx (nameof patchPrompt) "This new inspiration url already has another deviation."
            else
                upsertLocalDeviation ctx persistence deviation
        |> tryCatch (nameof patchLocalDeviation)
        
    let rec inspiration2Prompt (persistence: IPersistence) =
        
        fun ctx ->
            let payload = ctx.request |> asJson<Inspiration2Prompt>
            
            let inspiration = persistence.Find(dbKey_Inspirations, payload.Inspiration.ToString()) |> Option.get
            
            let prompt: Prompt = {
                Id = Guid.NewGuid()
                Timestamp = DateTimeOffset.Now
                Inspiration = Some inspiration
                Text = payload.Text
            }
            
            persistence.Delete(dbKey_Inspirations, inspiration.Url.ToString()) |> ignore
            persistence.Insert(dbKey_Prompts, prompt.Id.ToString(), prompt)
            
            prompt |> asOkJsonResponse ctx
        |> tryCatch (nameof inspiration2Prompt)
        
    let rec prompt2Deviation (persistence: IPersistence) =
        
        fun ctx ->
            let payload = ctx.request |> asJson<Prompt2LocalDeviation>
            
            let prompt = persistence.Find(dbKey_Prompts, payload.Prompt.ToString()) |> Option.get
            
            let deviation : LocalDeviation = {
                ImageUrl = payload.ImageUrl
                Timestamp = DateTimeOffset.Now
                Metadata = Metadata.defaults
                Origin = DeviationOrigin.Prompt prompt
            }
            
            persistence.Delete(dbKey_Prompts, prompt.Id.ToString()) |> ignore
            persistence.Insert(dbKey_LocalDeviations, deviation.ImageUrl.ToString(), deviation)
            
            deviation |> asOkJsonResponse ctx
        |> tryCatch (nameof prompt2Deviation)
        
    let rec prompt2Stash (persistence: IPersistence) =
        
        fun ctx ->
            let payload = ctx.request |> asJson<Prompt2LocalDeviation>
            
            let prompt = persistence.Find(dbKey_Prompts, payload.Prompt.ToString()) |> Option.get
            
            let deviation : LocalDeviation = {
                ImageUrl = payload.ImageUrl
                Timestamp = DateTimeOffset.Now
                Metadata = Metadata.defaults
                Origin = DeviationOrigin.Prompt prompt
            }
            
            persistence.Delete(dbKey_Prompts, prompt.Id.ToString()) |> ignore
            persistence.Insert(dbKey_LocalDeviations, deviation.ImageUrl.ToString(), deviation)
            
            deviation |> asOkJsonResponse ctx
        |> tryCatch (nameof prompt2Deviation)
        
    let rec stash (persistence: IPersistence) apiClient =
        
        fun ctx ->
            let key = getKey ctx
            
            let deviation = persistence.Find<string, LocalDeviation>(dbKey_LocalDeviations, key)
            
            match deviation with
            | None ->
                badRequestMessage ctx (nameof stash) $"No local deviation found for key '{key}'"
            
            | Some local ->
                let fileName = Uri key |> FunSharp.Common.Uri.lastSegment |> HttpUtility.UrlDecode
                let imagePath = $"{imagesLocation}\\{fileName}"
                let mimeType = Helpers.mimeType imagePath
                
                File.readAllBytesAsync imagePath
                |> Async.bind (fun imageContent ->
                    printfn $"Submitting '{LocalDeviation.keyOf local}' to stash..."
                    
                    submitToStash apiClient imageContent mimeType local
                )
                |> Async.bind (fun stashedDeviation ->
                    persistence.Delete(dbKey_LocalDeviations, key) |> ignore
                    persistence.Insert(dbKey_StashedDeviations, key, stashedDeviation)
                    
                    // printfn "Submission done!"
                    
                    stashedDeviation |> asOkJsonResponse ctx
                )
        |> tryCatch (nameof stash)
        
    let rec publish (persistence: IPersistence) apiClient secrets =
        
        fun ctx ->
            let key = getKey ctx
            
            match persistence.Find<string, StashedDeviation>(dbKey_StashedDeviations, key) with
            | None ->
                badRequestMessage ctx (nameof publish) $"stashed deviation '{key}' not found"
            
            | Some stashedDeviation ->
                printfn $"Publishing '{key}' from stash..."
                
                let galleryId =
                    match stashedDeviation.Metadata.Gallery.Trim() with
                    | "" -> galleryId secrets "RandomPile"
                    | x -> galleryId secrets x
                
                publishFromStash apiClient galleryId stashedDeviation
                |> Async.bind (fun publishedDeviation ->
                    persistence.Delete(dbKey_StashedDeviations, key) |> ignore
                    persistence.Insert(dbKey_PublishedDeviations, key, publishedDeviation)
                    
                    // printfn "Publishing done!"
                    
                    publishedDeviation |> asOkJsonResponse ctx
                )
        |> tryCatch (nameof publish)

    let rec deleteInspiration (persistence: IPersistence) =
        
        deleteItem<Inspiration> persistence dbKey_Inspirations (nameof deleteInspiration)

    let rec deletePrompt (persistence: IPersistence) =
        
        deleteItem<Prompt> persistence dbKey_Prompts (nameof deletePrompt)

    let rec deleteLocalDeviation (persistence: IPersistence) =
        
        deleteItem<LocalDeviation> persistence dbKey_LocalDeviations (nameof deleteLocalDeviation)
        
    let rec deleteStashedDeviation (persistence: IPersistence) =
        
        deleteItem<StashedDeviation> persistence dbKey_StashedDeviations (nameof deleteStashedDeviation)
