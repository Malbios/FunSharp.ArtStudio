namespace FunSharp.DeviantArt.Server

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
    let dbKey_LocalDeviations = "LocalDeviations"
    
    [<Literal>]
    let dbKey_StashedDeviations = "StashedDeviations"
    
    [<Literal>]
    let dbKey_PublishedDeviations = "PublishedDeviations"

    [<Literal>]
    let dbName = "FunSharp.DeviantArt.Manager"

    let submitToStash (client: Client) title (file: HttpUpload) =

        let submission = {
            StashSubmission.defaults with
                Title = title
        }

        printfn $"title: {title}"
        printfn $"mime: {file.mimeType}"
        printfn $"tempFilePath: {file.tempFilePath}"

        File.readAllBytesAsync file.tempFilePath
        |> Async.bind (fun content ->
            let httpFile: Http.File = {
                MediaType = Some file.mimeType
                Content = content
            }

            printfn "Submitting to stash..."

            client.SubmitToStash(submission, httpFile)
            |> AsyncResult.getOrFail
        )
        
    let asOkJsonResponse data =
        
        data
        |> JsonSerializer.serialize
        |> OK
        >=> setHeader "Content-Type" "application/json"

    let stashUrl itemId =
        
        $"https://sta.sh/0{Base36.encode itemId}"
        
    let asJson<'T> request =
        
        request.rawForm
        |> Encoding.UTF8.GetString
        |> JsonSerializer.deserialize<'T>
