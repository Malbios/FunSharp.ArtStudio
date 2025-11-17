namespace FunSharp.ArtStudio.Server.BackgroundTasks

open System
open FunSharp.Common
open FunSharp.Data.Abstraction
open FunSharp.ArtStudio.Model
open FunSharp.ArtStudio.Server.Helpers

[<RequireQualifiedAccess>]
module Inspiration =
    
    let private processItem serverAddress serverPort (persistence: IPersistence) (deviantArtClient: FunSharp.DeviantArt.Api.Client) (url: Uri) = async {
        
        printfn $"processing Inspiration task: {url.ToString()}"
        
        match urlAlreadyExists persistence url with
        | true ->
            printfn $"This inspiration url already exists in the database: {url}"
            
        | false ->
            let! id = deviantArtClient.GetDeviationId url |> Async.getOrFail
            let! deviation = deviantArtClient.GetDeviation id |> Async.getOrFail
            
            let fileName = $"{id}.jpg"
            
            let! imageContent = deviantArtClient.DownloadFile(deviation.preview.src)
            do! File.writeAllBytesAsync $"{imagesLocation}/{fileName}" imageContent
            
            let imageUrl = imageUrl serverAddress serverPort fileName
            
            let inspiration = {
                Url = url
                Timestamp = DateTimeOffset.Now
                ImageUrl = Some imageUrl
            }
            
            let task : ChatGPTTask = {
                Id = Guid.NewGuid()
                Timestamp = DateTimeOffset.Now
                Inspiration = inspiration
            }
            
            persistence.Insert(dbKey_BackgroundTasks, task.Id.ToString(), task |> BackgroundTask.ChatGPT)
    }
    
    let processTasks serverAddress serverPort (persistence: IPersistence) (deviantArtClient: FunSharp.DeviantArt.Api.Client) =
        
        let pendingTasks =
            persistence.FindAll<BackgroundTask>(dbKey_BackgroundTasks)
            |> Array.choose (function Inspiration url -> Some url | _ -> None)
        
        let cleanup (key: string) =
            persistence.Delete(dbKey_BackgroundTasks, key) |> ignore
            
            let remainingTasks = pendingTasks.Length - 1 |> fun x -> Math.Max(0, x)
            printfn $"processed BackgroundTask.Inspiration ({remainingTasks} remaining)"
            
            Async.returnM ()
            
        match pendingTasks |> Array.tryHead with
        | None -> Async.returnM ()
        | Some url ->
            processItem serverAddress serverPort persistence deviantArtClient (url |> Uri)
            |> Async.bind (fun _ -> cleanup url)
