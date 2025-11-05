namespace FunSharp.ArtStudio.Server

open System
open FunSharp.Common
open FunSharp.Data.Abstraction
open FunSharp.ArtStudio.Model
open FunSharp.ArtStudio.Server.Helpers

module BackgroundTasks =
        
    let private processNewInspirationTask serverAddress serverPort (persistence: IPersistence) (deviantArtClient: FunSharp.DeviantArt.Api.Client) (url: Uri) = async {
        
        // printfn $"starting to process new inspiration task: {url}"
        
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
        
        persistence.Insert(dbKey_Inspirations, url, inspiration)
        
        // printfn $"finished new inspiration task: {url}"
    }
            
    let private processSoraTask (persistence: IPersistence) (soraClient: FunSharp.OpenAI.Api.Sora.Client) (task: SoraTask) = async {
        
        printfn $"processing sora task: {task.Id}"
        
        let! result = soraClient.CreateImage(task.Prompt.Text, task.AspectRatio)
        
        match result with
        | Error errorMessage ->
            printfn $"SoraTask failed: {errorMessage}"
            
        | Ok taskResult ->
            
            let imageUrls =
                taskResult.Files
                |> Array.map (fun imagePath ->
                    imagePath
                    |> String.split '/'
                    |> List.last
                    |> fun x -> $"http://127.0.0.1:5123/automated/{x}"
                    |> Uri
                )
            
            let soraResult = {
                Id = Guid.NewGuid()
                Timestamp = DateTimeOffset.Now
                Task = task
                Images = imageUrls
            }
            
            persistence.Insert(dbKey_SoraResults, soraResult.Id, soraResult)
    }
    
    let processNewInspirationBackgroundTasks serverAddress serverPort (persistence: IPersistence) (deviantArtClient: FunSharp.DeviantArt.Api.Client) =
        
        let pendingTasks =
            persistence.FindAll<BackgroundTask>(dbKey_BackgroundTasks)
            |> Array.choose (function Inspiration url -> Some url | _ -> None)
            |> Array.sort
        
        let cleanup (key: string) =
            persistence.Delete(dbKey_BackgroundTasks, key) |> ignore
            
            let remainingTasks = pendingTasks.Length - 1 |> fun x -> Math.Max(0, x)
            printfn $"processed BackgroundTask.Inspiration ({remainingTasks} remaining)"
            
            Async.returnM ()
            
        match pendingTasks |> Array.tryHead with
        | None -> Async.returnM ()
        | Some url ->
            processNewInspirationTask serverAddress serverPort persistence deviantArtClient (url |> Uri)
            |> Async.bind (fun _ -> cleanup url)
    
    let processSoraBackgroundTasks (persistence: IPersistence) (soraClient: FunSharp.OpenAI.Api.Sora.Client) =
            
        let pendingTasks =
            persistence.FindAll<BackgroundTask>(dbKey_BackgroundTasks)
            |> Array.choose (function Sora task -> Some task | _ -> None)
            |> Array.sortBy _.Timestamp.Ticks.ToString()
        
        let cleanup (task: SoraTask) =
            persistence.Delete(dbKey_BackgroundTasks, task.Id.ToString()) |> ignore
            
            let remainingTasks = pendingTasks.Length - 1 |> fun x -> Math.Max(0, x)
            printfn $"processed BackgroundTask.Sora ({remainingTasks} remaining)"
            
            Async.returnM ()
            
        match pendingTasks |> Array.tryHead with
        | None -> Async.returnM ()
            
        | Some task ->
            processSoraTask persistence soraClient task
            |> Async.bind (fun _ -> cleanup task)
