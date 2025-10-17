namespace FunSharp.OpenAI.Api

open System
open System.Diagnostics
open System.Net.Http
open System.Text
open FunSharp.Common
open FunSharp.OpenAI.Api.Model.Sora

module Sora =
    
    [<Literal>]
    let private puppeteerPath = "C:/dev/fsharp/DeviantArt/Utilities/puppeteer"
    
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

        proc.Start() |> ignore
        proc.BeginOutputReadLine()
        proc.BeginErrorReadLine()

        exited.Publish
        |> Async.AwaitEvent
        |> Async.map (fun _ ->
            try
                if proc.ExitCode = 0 then
                    output.ToString().Trim()
                else
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
            let bearer = JsonSerializer.deserialize<BearerToken> output
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
        
    let private runScript_DeleteForever authTokens (generationIds: string array) =
        
        [| authTokens.Sentinel; authTokens.Bearer; JsonSerializer.serialize generationIds |]
        |> runScript $"{puppeteerPath}/delete-forever.js"
        |> Async.ignore
        
    let deserializeResponse<'T> value =
        match JsonSerializer.tryDeserialize<'T> value, JsonSerializer.tryDeserialize<ErrorContainer> value with
        | Some object, _ ->
            object
        | None, Some error ->
            failwith $"{error.error.message}"
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
        
        member _.CreateImage(prompt: string, variant) =
            
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
            |> JsonSerializer.serialize
            |> (runScript_CreateImage authTokens)
            |> Async.map deserializeResponse<Task>
            |> Async.map _.id
            
        member _.CheckTask(taskId) =
            
            runScript_CheckTask authTokens taskId
            |> Async.map deserializeResponse<TaskDetails>
            
        member _.GetTasks() =
            
            runScript_GetTasks authTokens
            |> Async.map deserializeResponse<TaskDetails array>
            
        member _.DeleteGenerations(generationIds: string array) =
            
            match generationIds.Length with
            | 0 -> Async.returnM ()
            | _ -> runScript_DeleteForever authTokens generationIds

        member _.DownloadImage(imageUrl: string) =
            
            imageUrl
            |> httpClient.GetByteArrayAsync
            |> Async.AwaitTask

        interface IDisposable with
        
            member this.Dispose() =

                httpClient.Dispose()
        
