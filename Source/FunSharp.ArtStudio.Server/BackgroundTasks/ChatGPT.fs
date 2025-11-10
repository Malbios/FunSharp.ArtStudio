namespace FunSharp.ArtStudio.Server.BackgroundTasks

open System
open FunSharp.Common
open FunSharp.Data.Abstraction
open FunSharp.ArtStudio.Model
open FunSharp.ArtStudio.Server.Helpers

[<RequireQualifiedAccess>]
module ChatGPT =
    
    let private processItem (persistence: IPersistence) (soraClient: FunSharp.OpenAI.Api.Sora.Client) (task: ChatGPTTask) = async {
        
        printfn $"processing ChatGPT task: {task.Id}"
        
        match task.Inspiration.ImageUrl with
        | None ->
            printfn $"Cannot create image2prompt (via ChatGPT) task from an inspiration without image: {task.Inspiration.Url}"
            
            return TaskResult.Skip
            
        | Some imageUrl ->
            let imageFilePath = imageUrl.ToString().Replace("http://127.0.0.1:5123", "C:/Files/FunSharp.DeviantArt")
            
            let! result = soraClient.Image2Prompt(imageFilePath)
            
            match result with
            | Error errorMessage ->
                
                if errorMessage.Contains("no output") then
                    printfn $"ChatGPTTask '{task.Id}' failed: {errorMessage}, skipping..."
                    return TaskResult.Skip
                else
                    printfn $"ChatGPTTask failed: {errorMessage}, retrying later..."
                    return TaskResult.Failed
                
            | Ok promptText ->
                
                let chatGPTResult = {
                    Id = Guid.NewGuid()
                    Timestamp = DateTimeOffset.Now
                    Task = task
                    Text = promptText
                }
                
                persistence.Insert(dbKey_ChatGPTResults, chatGPTResult.Id, chatGPTResult)
                
                return TaskResult.Succeeded
    }
    
    let processTasks (persistence: IPersistence) (soraClient: FunSharp.OpenAI.Api.Sora.Client) =
            
        let pendingTasks =
            persistence.FindAll<BackgroundTask>(dbKey_BackgroundTasks)
            |> Array.choose (function ChatGPT task -> Some task | _ -> None)
            |> Array.sortBy _.Timestamp.Ticks.ToString()
            
        let cleanup (taskResult: TaskResult) (task: ChatGPTTask) =
            match taskResult with
            | TaskResult.Failed -> ()
            
            | TaskResult.Skip
            | TaskResult.Succeeded ->
                persistence.Delete(dbKey_BackgroundTasks, task.Id.ToString()) |> ignore
            
                let remainingTasks = pendingTasks.Length - 1 |> fun x -> Math.Max(0, x)
                printfn $"processed BackgroundTask.ChatGPT ({remainingTasks} remaining)"

            Async.returnM ()
            
        match pendingTasks |> Array.tryHead with
        | None -> Async.returnM ()
            
        | Some task ->
            processItem persistence soraClient task
            |> Async.bind (fun taskResult -> cleanup taskResult task)
