namespace FunSharp.ArtStudio.Server.BackgroundTasks

open System
open FunSharp.Common
open FunSharp.Data.Abstraction
open FunSharp.ArtStudio.Model
open FunSharp.ArtStudio.Server.Helpers

[<RequireQualifiedAccess>]
module Sora =
    
    let private processItem (persistence: IPersistence) (soraClient: FunSharp.OpenAI.Api.Sora.Client) (task: SoraTask) = async {
        
        printfn $"processing sora task: {task.Id}"
        
        let! result = soraClient.CreateImage(task.Prompt.Text, task.AspectRatio)
        
        match result with
        | Error errorMessage ->
            printfn $"SoraTask failed: {errorMessage}"
            
            return TaskResult.Failed
            
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
                Images = imageUrls |> Array.append task.ExistingImages
            }
            
            persistence.Insert(dbKey_SoraResults, soraResult.Id, soraResult)
            
            return TaskResult.Succeeded
    }
    
    let processTasks (persistence: IPersistence) (soraClient: FunSharp.OpenAI.Api.Sora.Client) =
            
        let pendingTasks =
            persistence.FindAll<BackgroundTask>(dbKey_BackgroundTasks)
            |> Array.choose (function Sora task -> Some task | _ -> None)
            |> Array.sortBy _.Timestamp.Ticks.ToString()
        
        let cleanup (taskResult: TaskResult) (task: SoraTask) =
            match taskResult with
            | TaskResult.Failed -> ()
            
            | TaskResult.Skip
            | TaskResult.Succeeded ->
                persistence.Delete(dbKey_BackgroundTasks, task.Id.ToString()) |> ignore
            
                let remainingTasks = pendingTasks.Length - 1 |> fun x -> Math.Max(0, x)
                printfn $"processed BackgroundTask.Sora ({remainingTasks} remaining)"

            Async.returnM ()
            
        match pendingTasks |> Array.tryHead with
        | None -> Async.returnM ()
            
        | Some task ->
            processItem persistence soraClient task
            |> Async.bind (fun taskResult -> cleanup taskResult task)
