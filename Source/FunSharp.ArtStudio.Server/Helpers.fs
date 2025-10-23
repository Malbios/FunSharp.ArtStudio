namespace FunSharp.ArtStudio.Server

open System
open System.Text
open System.Web
open FunSharp.Http
open Microsoft.AspNetCore.StaticFiles
open Microsoft.FSharp.Collections
open Suave
open Suave.Operators
open Suave.RequestErrors
open Suave.Successful
open Suave.Writers
open FunSharp.Common
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model
open FunSharp.ArtStudio.Model

module Helpers =
    
    [<Literal>]
    let dbKey_Inspirations = "Inspirations"
    
    [<Literal>]
    let dbKey_Prompts = "Prompts"
    
    [<Literal>]
    let dbKey_LocalDeviations = "LocalDeviations"
    
    [<Literal>]
    let dbKey_StashedDeviations = "StashedDeviations"
    
    [<Literal>]
    let dbKey_PublishedDeviations = "PublishedDeviations"
    
    [<Literal>]
    let dbKey_DeletedItems = "DeletedItems"
    
    [<Literal>]
    let dbKey_BackgroundTasks = "BackgroundTasks"
    
    [<Literal>]
    let imagesLocation = @"C:\Files\FunSharp.DeviantArt\images"
        
    let private asStashed (local: LocalDeviation) (response: StashSubmissionResponse) =
        
        match response.status with
        | "success" ->
            {
                ImageUrl = local.ImageUrl
                Timestamp = DateTimeOffset.Now
                StashId = response.itemid
                Origin = local.Origin
                Metadata = local.Metadata
            }
        | _ ->
            failwith $"Failed to stash {local.ImageUrl}"
        
    let private asPublished (stashed: StashedDeviation) (response: PublishResponse) =
        
        match response.status with
        | "success" ->
            {
                ImageUrl = stashed.ImageUrl
                Timestamp = DateTimeOffset.Now
                Url = Uri response.url
                Origin = stashed.Origin
                Metadata = stashed.Metadata
            }
        | _ ->
            failwith $"Failed to publish {stashed.ImageUrl}"

    let submitToStash (client: Client) imageContent mimeType (local: LocalDeviation) =
        
        let submission = {
            StashSubmission.defaults with
                title = local.Metadata.Title
        }
        
        let file = File.InMemory (imageContent, Some mimeType)
        
        client.SubmitToStash(submission, file)
        |> Async.getOrFail
        |> Async.map (asStashed local)
        
    let publishFromStash (client: Client) (galleryId: string) (stashed: StashedDeviation) =
        
        let submission = {
            PublishSubmission.defaults with
                itemid = stashed.StashId
                is_mature = stashed.Metadata.IsMature
                galleryids = [| galleryId |]
        }
        
        client.PublishFromStash(submission)
        |> Async.getOrFail
        |> Async.map (asPublished stashed)
        
    let asOkJsonResponse ctx data =
        
        data
        |> JsonSerializer.serialize
        |> OK
        >=> setHeader "Content-Type" "application/json"
        <| ctx
        
    let asString request =
        
        request.rawForm
        |> Encoding.UTF8.GetString
        
    let asJson<'T> request =
        
        request
        |> asString
        |> JsonSerializer.deserialize<'T>

    let mimeType filePath =
        
        let provider = FileExtensionContentTypeProvider()
        
        match provider.TryGetContentType(filePath) with
        | true, mime -> mime
        | false, _ -> "application/octet-stream"
        
    let private badRequest (ctx: HttpContext) (id: string) (message: string option) (ex: exn option) =
        
        printfn $"ERROR ({id}):"
        
        match message with
        | None -> ()
        | Some message -> printfn $"{message}"
        
        match ex with
        | None -> ()
        | Some ex ->
            printfn $"{ex.Message}"
            printfn $"{ex.StackTrace}"
            
        let responseMessage =
            match message, ex with
            | Some message, _ -> message
            | _, Some ex -> ex.Message
            | _ -> ""
            
        BAD_REQUEST responseMessage ctx

    let badRequestMessage (ctx: HttpContext) (id: string) (message: string) =
        
        badRequest ctx id (Some message) None

    let badRequestException (ctx: HttpContext) (id: string) (ex: exn) =
        
        badRequest ctx id None (Some ex)
        
    let imageUrl serverAddress serverPort fileName =
        
        Uri $"http://{serverAddress}:{serverPort}/images/{fileName}"
        
    let galleryId secrets galleryName =
        
        secrets.galleries
        |> Array.find(fun x -> x.name = galleryName)
        |> _.id
        
    let urlAlreadyExists (persistence: IPersistence) (url: Uri) =
        
        let inspirationHasUrl inspiration =
            inspiration.Url.ToString() = url.ToString()
            
        let promptHasUrl prompt =
            match prompt.Inspiration with
            | None -> false
            | Some inspiration -> inspirationHasUrl inspiration
            
        let localHasUrl local =
            match local.Origin with
            | DeviationOrigin.None -> false
            | DeviationOrigin.Inspiration inspiration -> inspirationHasUrl inspiration
            | DeviationOrigin.Prompt prompt -> promptHasUrl prompt
            
        let stashedHasUrl (stashed: StashedDeviation) =
            match stashed.Origin with
            | DeviationOrigin.None -> false
            | DeviationOrigin.Inspiration inspiration -> inspirationHasUrl inspiration
            | DeviationOrigin.Prompt prompt -> promptHasUrl prompt
            
        let publishedHasUrl (published: PublishedDeviation) =
            match published.Origin with
            | DeviationOrigin.None -> false
            | DeviationOrigin.Inspiration inspiration -> inspirationHasUrl inspiration
            | DeviationOrigin.Prompt prompt -> promptHasUrl prompt

        (persistence.FindAny<Inspiration>(dbKey_Inspirations, inspirationHasUrl) |> Array.isEmpty |> not) ||
        (persistence.FindAny<Inspiration>(dbKey_DeletedItems, inspirationHasUrl) |> Array.isEmpty |> not) ||
        (persistence.FindAny<Prompt>(dbKey_Prompts, promptHasUrl) |> Array.isEmpty |> not) ||
        (persistence.FindAny<Prompt>(dbKey_DeletedItems, promptHasUrl) |> Array.isEmpty |> not) ||
        (persistence.FindAny<LocalDeviation>(dbKey_LocalDeviations, localHasUrl) |> Array.isEmpty |> not) ||
        (persistence.FindAny<LocalDeviation>(dbKey_DeletedItems, localHasUrl) |> Array.isEmpty |> not) ||
        (persistence.FindAny<StashedDeviation>(dbKey_StashedDeviations, stashedHasUrl) |> Array.isEmpty |> not) ||
        (persistence.FindAny<StashedDeviation>(dbKey_DeletedItems, stashedHasUrl) |> Array.isEmpty |> not) ||
        (persistence.FindAny<PublishedDeviation>(dbKey_PublishedDeviations, publishedHasUrl) |> Array.isEmpty |> not) ||
        (persistence.FindAny<PublishedDeviation>(dbKey_DeletedItems, publishedHasUrl) |> Array.isEmpty |> not)
        
    let private upsertItem<'Key, 'Value> (ctx: HttpContext) (persistence: IPersistence) (key: 'Key) dbKey (item: 'Value)=
        
        printfn $"Updating '{key}'..."
        
        do persistence.Upsert(dbKey, key, item) |> ignore
        
        printfn "Update done!"
        
        item |> asOkJsonResponse ctx
        
    let upsertPrompt (ctx: HttpContext) (persistence: IPersistence) (prompt: Prompt) =
        
        upsertItem<Guid, Prompt> ctx persistence (Prompt.keyOf prompt) dbKey_Prompts prompt
        
    let upsertLocalDeviation (ctx: HttpContext) (persistence: IPersistence) (deviation: LocalDeviation) =
        
        upsertItem<Uri, LocalDeviation> ctx persistence (LocalDeviation.keyOf deviation) dbKey_LocalDeviations deviation
        
    let tryCatch (id: string) action : WebPart =
        
        fun ctx ->
            try
                action ctx
            with ex ->
                badRequestException ctx id ex
        
    let getQueryParam (ctx: HttpContext) key =
        
        match ctx.request.queryParam key with
        | Choice1Of2 value -> Some value
        | _ -> None
        
    let getKey (ctx: HttpContext)=
        
        match getQueryParam ctx "key" with
        | None -> failwith "query param 'key' is missing"
        | Some v -> v |> HttpUtility.UrlDecode
        
    [<RequireQualifiedAccess>]
    type SortDirection =
        | Ascending
        | Descending
        
    [<RequireQualifiedAccess>]
    module SortDirection =
        
        let fromString (value: string) =
            
            match value.ToLower() with
            | "asc" -> SortDirection.Ascending
            | "desc" -> SortDirection.Descending
            | x -> failwith $"unexpected sort direction: '{x}'"
        
    type SortOptions = {
        Property: string
        Direction: SortDirection
    }
    
    let withPagination (ctx: HttpContext) (sort: SortOptions -> 'T array -> 'T array) (items: 'T array) =
        let offset =
            getQueryParam ctx "offset"
            |> Option.bind Int.tryParse
            |> Option.defaultValue 0

        let limit =
            getQueryParam ctx "limit"
            |> Option.bind Int.tryParse
            |> Option.defaultValue 50
            
        let sortBy =
            getQueryParam ctx "sortBy"
            |> Option.defaultValue "timestamp"
            
        let sortDirection =
            getQueryParam ctx "sortDir"
            |> Option.map SortDirection.fromString
            |> Option.defaultValue SortDirection.Ascending
            
        let sortOptions = { Property = sortBy; Direction = sortDirection }
        let sortedItems = sort sortOptions items
        
        let pageItems =
            sortedItems
            |> Array.skip offset
            |> Array.truncate limit
        
        {
            Page.empty with
                items = pageItems
                offset = offset
                total = items.Length
                has_more = offset + limit < items.Length
        }
        |> asOkJsonResponse ctx
        
    let rec deleteItem<'T when 'T: not struct and 'T: equality and 'T: not null> (persistence: IPersistence) dbKey (id: string) =
        
        fun ctx ->
            let key = getKey ctx
            
            match persistence.Find<string, 'T>(dbKey, key) with
            | None -> ()
            
            | Some item ->
                persistence.Delete(dbKey, key) |> ignore
                persistence.Insert(dbKey_DeletedItems, key, item)
                
            NO_CONTENT ctx
        |> tryCatch id
        
    let inspirationIsDuplicate persistence newInspiration existingInspiration =
        
        let urlAlreadyExists = urlAlreadyExists persistence
        
        match existingInspiration with
        | None -> urlAlreadyExists newInspiration.Url
        
        | Some existingInspiration ->
            if newInspiration.Url = existingInspiration.Url then false
            else urlAlreadyExists newInspiration.Url
            
    let promptHasDuplicateInspiration persistence newPrompt existingPrompt =
        
        match newPrompt.Inspiration with
        | None -> false
        
        | Some newInspiration ->
            let existingInspiration = existingPrompt |> Option.bind _.Inspiration
            inspirationIsDuplicate persistence newInspiration existingInspiration
            
    let originHasDuplicateInspiration persistence newOrigin (existingOrigin: DeviationOrigin option) =
        
        match newOrigin with
        | DeviationOrigin.None -> false
        
        | DeviationOrigin.Inspiration newInspiration ->
            match existingOrigin with
            | None -> inspirationIsDuplicate persistence newInspiration None
            
            | Some existingOrigin ->
                match existingOrigin with
                | DeviationOrigin.Inspiration existingInspiration ->
                    inspirationIsDuplicate persistence newInspiration (Some existingInspiration)
                    
                | _ -> inspirationIsDuplicate persistence newInspiration None
                
        | DeviationOrigin.Prompt newPrompt ->
            match existingOrigin with
            | None -> promptHasDuplicateInspiration persistence newPrompt None
            
            | Some existingOrigin ->
                match existingOrigin with
                | DeviationOrigin.Prompt existingPrompt ->
                    promptHasDuplicateInspiration persistence newPrompt (Some existingPrompt)
                    
                | _ -> promptHasDuplicateInspiration persistence newPrompt None
                
    let localDeviationHasDuplicateInspiration persistence newDeviation (existingDeviation: LocalDeviation option) =
        
        let existingOrigin = existingDeviation |> Option.map _.Origin
        
        originHasDuplicateInspiration persistence newDeviation.Origin existingOrigin
