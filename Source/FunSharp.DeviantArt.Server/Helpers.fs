namespace FunSharp.DeviantArt.Server

open System
open System.Text
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
    let dbKey_Images = "Images"

    [<Literal>]
    let dbName = "FunSharp.DeviantArt.Manager"
        
    let private asStashed (local: LocalDeviation) (response: StashSubmissionResponse) =
        
        match response.status with
        | "success" ->
            {
                StashId = response.item_id
                Metadata = local
            }
        | _ ->
            failwith $"Failed to stash {local.Title}"
        
    let private asPublished (stashed: StashedDeviation) (response: PublicationResponse) =
        
        match response.status with
        | "success" ->
            {
                Url = Uri response.url
                Metadata = stashed.Metadata
            }
        | _ ->
            failwith $"Failed to publish {stashed.Metadata.Title}"

    let submitToStash (client: Client) (local: LocalDeviation) (image: Image) =

        let submission = { StashSubmission.defaults with Title = local.Title }

        let httpFile: Http.File = {
            MediaType = Some image.MimeType
            Content = image.Content
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
