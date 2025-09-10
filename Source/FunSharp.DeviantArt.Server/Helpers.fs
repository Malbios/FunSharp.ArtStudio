namespace FunSharp.DeviantArt.Server

open System
open System.Text
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
    let dbKey_Settings = "Settings"
    
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
    let dbName = "FunSharp.DeviantArt.Manager"
    
    [<Literal>]
    let imagesLocation = @"C:\Files\FunSharp.DeviantArt\images"
        
    let private asStashed (local: LocalDeviation) (response: StashSubmissionResponse) =
        
        match response.status with
        | "success" ->
            {
                ImageUrl = local.ImageUrl
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
