namespace FunSharp.DeviantArt.Server

open System
open System.Text
open FunSharp.Data.Abstraction
open Microsoft.AspNetCore.StaticFiles
open Suave
open Suave.Operators
open Suave.RequestErrors
open Suave.Successful
open Suave.Writers
open FunSharp.Common
open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model

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
    let imagesLocation = @"C:\Files\FunSharp.DeviantArt\images"
        
    let private asStashed (local: LocalDeviation) (response: StashSubmissionResponse) =
        
        match response.status with
        | "success" ->
            {
                ImageUrl = local.ImageUrl
                Timestamp = DateTimeOffset.Now
                StashId = response.item_id
                Origin = local.Origin
                Metadata = local.Metadata
            }
        | _ ->
            failwith $"Failed to stash {local.ImageUrl}"
        
    let private asPublished (stashed: StashedDeviation) (response: PublicationResponse) =
        
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
                Title = local.Metadata.Title
        }
        
        let httpFile: Http.File = {
            MediaType = Some mimeType
            Content = imageContent
        }
        
        client.SubmitToStash(submission, httpFile)
        |> AsyncResult.getOrFail
        |> Async.map (asStashed local)
        
    let publishFromStash (client: Client) (galleryId: string) (stashed: StashedDeviation) =
        
        let submission = {
            PublishSubmission.defaults with
                IsMature = stashed.Metadata.IsMature
                Galleries = [| galleryId |]
                ItemId = stashed.StashId
        }
        
        client.PublishFromStash(submission)
        |> AsyncResult.getOrFail
        |> Async.map (asPublished stashed)
        
    let asOkJsonResponse data =
        
        data
        |> JsonSerializer.serialize
        |> OK
        >=> setHeader "Content-Type" "application/json"
        
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
        
    let inspirationUrlAlreadyExists (dataPersistence: IPersistence) (url: string) =
        
        let existingInspirations =
            dataPersistence.FindAny<Inspiration>(dbKey_Inspirations, (fun x -> x.Url.ToString() = url))
        let existingPrompts =
            dataPersistence.FindAny<Prompt>(dbKey_Prompts, (fun x ->
                x.Inspiration
                |> Option.map _.Url.ToString()
                |> Option.defaultValue ""
                |> fun x -> x = url
            ))
        let existingLocalDeviations =
            dataPersistence.FindAny<LocalDeviation>(dbKey_LocalDeviations, (fun x ->
                match x.Origin with
                | DeviationOrigin.Inspiration inspiration ->
                    inspiration.Url.ToString() = url
                | _ -> false
            ))
        let existingStashedDeviations =
            dataPersistence.FindAny<StashedDeviation>(dbKey_StashedDeviations, (fun x ->
                match x.Origin with
                | DeviationOrigin.Inspiration inspiration ->
                    inspiration.Url.ToString() = url
                | _ -> false
            ))
        let existingPublishedDeviations =
            dataPersistence.FindAny<PublishedDeviation>(dbKey_PublishedDeviations, (fun x ->
                match x.Origin with
                | DeviationOrigin.Inspiration inspiration ->
                    inspiration.Url.ToString() = url
                | _ -> false
            ))

        existingInspirations.Length > 0
        || existingPrompts.Length > 0
        || existingLocalDeviations.Length > 0
        || existingStashedDeviations.Length > 0
        || existingPublishedDeviations.Length > 0
