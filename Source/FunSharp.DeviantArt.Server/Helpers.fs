namespace FunSharp.DeviantArt.Server

open System
open System.Text
open Microsoft.AspNetCore.StaticFiles
open Suave
open Suave.Operators
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
        
        let submission = { StashSubmission.defaults with Title = local.Metadata.Title }
        
        let httpFile: Http.File = {
            MediaType = Some mimeType
            Content = imageContent
        }
        
        client.SubmitToStash(submission, httpFile)
        |> AsyncResult.getOrFail
        |> Async.map (asStashed local)
        
    let publishFromStash (client: Client) (stashed: StashedDeviation) =
        
        let submission = {
            PublishSubmission.defaults with
                ItemId = stashed.StashId
        }
        
        printfn $"Publishing '{stashed.Metadata.Title}' from stash..."

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
