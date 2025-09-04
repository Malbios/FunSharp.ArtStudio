namespace FunSharp.DeviantArt.Server

open System.IO
open FunSharp.Common
open FunSharp.DeviantArt.Api
open Suave
open Suave.Operators
open Suave.Successful
open Suave.Writers

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
    
    let readAllBytesAsync (path: string) : Async<byte[]> = async {
        use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize = 64 * 1024, options = FileOptions.Asynchronous)

        let buffer =
            match stream.Length with
            | length when length <= int64 System.Int32.MaxValue ->
                int length
            | _ ->
                failwith "The file is too large for a single buffer"
            |> Array.zeroCreate<byte>

        let! _ = stream.ReadAsync(buffer, 0, buffer.Length) |> Async.AwaitTask
        
        return buffer
    }

    let submitToStash (client: Client) title (file: Http.HttpUpload) =

        let submission = {
            StashSubmission.defaults with
                Title = title
        }

        printfn $"title: {title}"
        printfn $"mime: {file.mimeType}"
        printfn $"tempFilePath: {file.tempFilePath}"

        readAllBytesAsync file.tempFilePath
        |> Async.bind (fun content ->
            let httpFile: Http.File = {
                MediaType = Some file.mimeType
                Content = content
            }

            printfn "Submitting to stash..."

            client.SubmitToStash(submission, httpFile)
            |> AsyncResult.getOrFail
        )
        
    let jsonResponse data =
        data
        |> JsonSerializer.serialize
        |> OK
        >=> setHeader "Content-Type" "application/json"
