namespace FunSharp.ArtStudio.Server

open System
open System.Threading.Tasks
open FunSharp.OpenAI.Api.Model.Sora

module SoraTaskManager =
    
    let processTask (client: FunSharp.OpenAI.Api.Sora.Client) = async {
        do! client.UpdateAuthTokens()
        
        let prompt = "a stunningly beautiful young woman inviting the viewer into her home, pov, hyperrealistic"
        
        let! taskId = client.CreateImage(prompt, ImageType.Portrait)
        printfn "task started!"
        
        let mutable taskDetails = TaskDetails.empty
        let mutable taskIsDone = false
        while (not taskIsDone) do
            let! newTaskDetails = client.CheckTask(taskId)
            taskDetails <- newTaskDetails
            
            if taskDetails.status = TaskStatus.Running || taskDetails.status = TaskStatus.PreProcessing then
                do! Task.Delay(5000)
                printfn "waiting..."
            else
                printfn "waiting is done!"
                printfn $"task status: {taskDetails.status}"
                taskIsDone <- true
                
        for generation in taskDetails.generations do
            let fileName = $"C:/Files/FunSharp.DeviantArt/automated/{Guid.NewGuid ()}.png"
            let! fileContent = client.DownloadImage(generation.url)
            do! FunSharp.Common.File.writeAllBytesAsync fileName fileContent
    }
