namespace FunSharp.OpenAI.Api

open System
open System.Diagnostics
open System.Net.Http
open System.Text
open System.Text.Json
open System.Threading.Tasks
open FunSharp.Common
open FunSharp.Common.JsonSerializer
open FunSharp.OpenAI.Api.Model.Sora

module Sora =
    
    [<Literal>]
    let private puppeteerPath = "C:/dev/fsharp/FunSharp.ArtStudio/Utilities/puppeteer"
    
    [<Literal>]
    let private imageOutputPath = "C:/Files/FunSharp.DeviantArt/automated"
    
    let private generateEndpoint = "https://sora.chatgpt.com/backend/video_gen"
    let private checkTaskEndpoint taskId = $"https://sora.chatgpt.com/backend/video_gen/{taskId}"
    
    let private runScript scriptPath (arguments: string array) =
        
        let psi =
            ProcessStartInfo(
                FileName = "node",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            )
            
        psi.ArgumentList.Add(scriptPath)
        
        for argument in arguments do
            psi.ArgumentList.Add(argument)

        let proc = new Process(StartInfo = psi)
        let output = StringBuilder()
        let error = StringBuilder()

        proc.OutputDataReceived.Add(fun args ->
            if not (isNull args.Data) then output.AppendLine(args.Data) |> ignore)

        proc.ErrorDataReceived.Add(fun args ->
            if not (isNull args.Data) then error.AppendLine(args.Data) |> ignore)

        let exited = Event<unit>()
        proc.Exited.Add(fun _ -> exited.Trigger())
        proc.EnableRaisingEvents <- true
        
        // printfn $"Starting script: {scriptPath}"

        proc.Start() |> ignore
        proc.BeginOutputReadLine()
        proc.BeginErrorReadLine()

        exited.Publish
        |> Async.AwaitEvent
        |> Async.map (fun _ ->
            try
                let output = output.ToString().Trim()
                
                if proc.ExitCode = 0 then
                    match output with
                    | output when output = "" ->
                        failwith $"Script {scriptPath} failed: no output"
                        
                    | output ->
                        match tryDeserialize<ErrorContainer> output with
                        | Some error ->
                            failwith $"Script {scriptPath} failed: {error.error.message}"
                            
                        | None ->
                            // printfn $"Script {scriptPath} is done!"
                            // printfn $"{output}"
                            output
                else
                    printfn $"Script {scriptPath} failed with exit code '{proc.ExitCode}': {error.ToString()}"
                    printfn $"{output}"
                    failwith (error.ToString())
            finally
                proc.Dispose()
        )
        
    let private runScript_SentinelAndCookies () =
        
        Array.empty
        |> runScript $"{puppeteerPath}/sentinel-and-cookies.js"
        |> Async.map (fun output ->
            let lines = output.Trim().Split(Environment.NewLine)
            (lines[0], lines[1])
        )
        
    let private runScript_BearerToken cookies =
        
        [|cookies|]
        |> runScript $"{puppeteerPath}/bearer.js"
        |> Async.map (fun output ->
            let bearer = deserialize<BearerToken> output
            bearer.accessToken
        )
        
    let private runScript_CreateImage authTokens body =
        
        [| authTokens.Sentinel; authTokens.Bearer; body |]
        |> runScript $"{puppeteerPath}/create-image.js"
        
    let private runScript_CheckTask authTokens taskId =
        
        [| authTokens.Sentinel; authTokens.Bearer; taskId |]
        |> runScript $"{puppeteerPath}/check-task.js"
        
    let private runScript_GetTasks authTokens =
        
        [| authTokens.Sentinel; authTokens.Bearer |]
        |> runScript $"{puppeteerPath}/get-tasks.js"

    let serializerOptionsCustomizer (options: JsonSerializerOptions) =
        options.Converters.Add(NullTolerantFloatConverter())
        options.Converters.Add(CaseInsensitiveEnumConverter<TaskStatus>())
        options.Converters.Add(CaseInsensitiveEnumConverter<ModerationStatus>())
        
        options
        
    let deserializeResponse<'T> value =
        let v = tryCustomDeserialize<'T> serializerOptionsCustomizer value
        let e = tryCustomDeserialize<ErrorContainer> serializerOptionsCustomizer value
        
        match v, e with
        | Some value, _ ->
            value
        | None, Some error ->
            failwith $"error response: {error.error.message}"
        | _ ->
            failwith $"could not deserialize this value:\n\n{value}"
        
    type Client() =
        
        let httpClient = new HttpClient()
        
        let mutable authTokens = AuthenticationTokens.empty
        
        member _.UpdateAuthTokens() =
            
            runScript_SentinelAndCookies ()
            |> Async.bind (fun (sentinelToken, cookies) ->
                runScript_BearerToken cookies
                |> Async.map (fun bearerToken ->
                    authTokens <- {
                        Sentinel = sentinelToken
                        Cookies = cookies
                        Bearer = bearerToken
                    }
                )
            )
        
        member _.StartTask(prompt: string, variant) =
            
            let prompt =
                prompt.Split([| "\r\n"; "\n" |], StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
                |> String.concat "\n\n"
            
            let width =
                match variant with
                | Landscape -> 720
                | Portrait
                | Square -> 480
                
            let height =
                match variant with
                | Portrait -> 720
                | Square
                | Landscape -> 480
                
            {|
                ``type`` = "image_gen"
                operation = "simple_compose"
                prompt = prompt
                n_variants = 2
                width = width
                height = height
                n_frames = 1
                inpaint_items = []
            |}
            |> serialize
            |> (runScript_CreateImage authTokens)
            |> Async.map deserializeResponse<TaskResponse>
            |> Async.map _.id
            
        member _.CheckTask(taskId) =
            
            runScript_CheckTask authTokens taskId
            |> Async.map deserializeResponse<TaskDetails>
            
        member _.GetTasks() =
            
            runScript_GetTasks authTokens
            |> Async.map deserializeResponse<TaskDetails array>
            
        member _.DeleteTask(task: TaskDetails) =
            
            let generationIds = [|for generation in task.generations do generation.id|]
            
            [| authTokens.Sentinel; authTokens.Bearer; serialize task.id; serialize generationIds |]
            |> runScript $"{puppeteerPath}/delete-task.js"
            |> Async.ignore

        member _.DownloadImage(imageUrl: string) =
            
            imageUrl
            |> httpClient.GetByteArrayAsync
            |> Async.AwaitTask
            
        member this.WaitForFinish(taskId: string, delay: TimeSpan) = async {
            
            let mutable taskDetails = TaskDetails.empty
            let mutable taskIsDone = false
            
            while (not taskIsDone) do
                let! newDetails = this.CheckTask(taskId)
                taskDetails <- newDetails
                
                match taskDetails.status with
                | TaskStatus.PreProcessing
                | TaskStatus.Queued
                | TaskStatus.Running ->
                    printfn "waiting for task..."
                    do! Task.Delay(delay.TotalMilliseconds |> int)
                | _ ->
                    taskIsDone <- true
            
            return taskDetails
        }
        
        member this.WaitForFinish(taskId: string) =
            
            this.WaitForFinish(taskId, TimeSpan.FromSeconds(30))
            
        member this.CreateImage(prompt, aspectRatio) =
            
            let getFiles taskDetails =
                
                match taskDetails.status with
                | TaskStatus.Succeeded ->
                    printfn "downloading images..."
                    
                    taskDetails.generations
                    |> Seq.map (fun generation -> async {
                        let fileName = $"{imageOutputPath}/{Guid.NewGuid ()}.png"
                        let! fileContent = this.DownloadImage(generation.url)
                        do! File.writeAllBytesAsync fileName fileContent
                        return fileName
                    })
                    |> Async.Parallel
                    |> Ok

                | _ -> Error taskDetails.status
            
            async {
                printfn "updating auth tokens..."
                do! this.UpdateAuthTokens()
                
                printfn "starting task..."
                let! taskId = this.StartTask(prompt, aspectRatio)
                let! taskDetails = this.WaitForFinish(taskId)
                
                let result = getFiles taskDetails
                
                printfn "deleting task..."
                do! this.DeleteTask(taskDetails)
                
                match result with
                | Ok filesResult ->
                    let! files = filesResult
                    return Ok { Files = files }
                    
                | Error taskStatus ->
                    return Error taskStatus
            }
            
        member _.Image2Prompt(imageFilePath) =
            
            try
                [| imageFilePath |]
                |> runScript $"{puppeteerPath}/image2prompt.js"
                |> Async.map _.Trim()
                |> Async.Catch
                |> Async.map (function
                    | Choice1Of2 result -> Ok result
                    | Choice2Of2 exn -> Error exn.Message)
                
            with exn ->
                exn.Message |> Error |> Async.returnM
            
        member _.UpdateStash(stashUrl, inspirationUrl) =
            
            try
                [| stashUrl; inspirationUrl |]
                |> runScript $"{puppeteerPath}/update-DA-stash.js"
                |> Async.map _.Trim()
                |> Async.Catch
                |> Async.map (function
                    | Choice1Of2 result -> Ok result
                    | Choice2Of2 exn -> Error exn.Message)
                
            with exn ->
                exn.Message |> Error |> Async.returnM

        interface IDisposable with
        
            member this.Dispose() =

                httpClient.Dispose()
        
