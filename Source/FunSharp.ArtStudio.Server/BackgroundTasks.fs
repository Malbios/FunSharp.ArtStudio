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
        
    let processBackgroundTasks serverAddress serverPort (persistence: IPersistence) (deviantArtClient: FunSharp.DeviantArt.Api.Client) (soraClient: FunSharp.OpenAI.Api.Sora.Client) =
            
        let sortTasksByKind_NewInspiration_Sora =
            function
            | Inspiration u -> 0, u
            | Sora soraTask -> 1, soraTask.Timestamp.Ticks.ToString()
            
        let pendingTasks =
            persistence.FindAll<BackgroundTask>(dbKey_BackgroundTasks)
            |> Array.sortBy sortTasksByKind_NewInspiration_Sora
        
        let cleanup (key: string) (task: BackgroundTask) =
            persistence.Insert(dbKey_DeletedItems, key, task)
            persistence.Delete(dbKey_BackgroundTasks, key) |> ignore
            
            let remainingTasks = pendingTasks.Length - 1 |> fun x -> Math.Max(0, x)
            printfn $"processed {Union.toString task} ({remainingTasks} remaining)"
            
            Async.returnM ()
            
        match pendingTasks |> Array.tryHead with
        | None -> Async.returnM ()
            
        | Some task ->
            match task with
            | Inspiration url ->
                processNewInspirationTask serverAddress serverPort persistence deviantArtClient (url |> Uri)
                |> Async.bind (fun _ -> cleanup url task)
                
            | Sora soraTask ->
                processSoraTask persistence soraClient soraTask
                |> Async.bind (fun _ -> cleanup (soraTask.Id.ToString()) task)
