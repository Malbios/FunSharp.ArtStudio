namespace FunSharp.OpenAI

open System
open System.Diagnostics
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open FunSharp.Common
open FunSharp.Http

module Sora =
    
    type ImageType =
        | Landscape
        | Square
        | Portrait
        
    type GenerateResponse = {
        id: string
    }
    
    let private generateEndpoint = "https://sora.chatgpt.com/backend/video_gen"
    let private checkTaskEndpoint taskId = $"https://sora.chatgpt.com/backend/video_gen/{taskId}"
        
    let private sentinelToken () =
        let psi =
            ProcessStartInfo(
                FileName = "node",
                Arguments = @"C:\dev\OpenAIModeration\puppeteer\auth.js",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            )

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
    
    let private addHeaders secrets (sentinelToken: string) (request: HttpRequestMessage) =
        
        request.Headers.Authorization <- AuthenticationHeaderValue("Bearer", secrets.bearer_token)
        
        request.Headers.TryAddWithoutValidation("OpenAI-Sentinel-Token", sentinelToken) |> ignore
        request.Headers.TryAddWithoutValidation("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36 Edg/140.0.0.0") |> ignore
        request.Headers.Add("accept", "*/*")
        request.Headers.Add("accept-language", "en-US,en;q=0.9,de;q=0.8")
        request.Headers.Add("cache-control", "no-cache")
        request.Headers.Add("pragma", "no-cache")
        request.Headers.Add("priority", "u=1, i")
        request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Google Chrome\";v=\"140\"")
        request.Headers.Add("sec-ch-ua-mobile", "?0")
        request.Headers.Add("sec-ch-ua-platform", "\"Windows\"")
        request.Headers.Add("sec-fetch-dest", "empty")
        request.Headers.Add("sec-fetch-mode", "cors")
        request.Headers.Add("sec-fetch-site", "same-origin")
        request.Headers.Add("Origin", "https://sora.chatgpt.com")
        request.Headers.Add("OAI-Device-Id", "211a54fd-6a35-4b89-9523-b1bb74748473")
        request.Headers.TryAddWithoutValidation("sec-ch-ua-arch", "\"x86\"") |> ignore
        request.Headers.TryAddWithoutValidation("sec-ch-ua-bitness", "\"64\"") |> ignore
        request.Headers.TryAddWithoutValidation("sec-ch-ua-platform-version", "\"19.0.0\"") |> ignore
        request.Headers.TryAddWithoutValidation("sec-ch-ua-full-version", "\"140.0.3485.94\"") |> ignore
        request.Headers.TryAddWithoutValidation("Cookie", secrets.cookies) |> ignore
        
        request.Headers.Referrer <- Uri("https://sora.chatgpt.com/trash")
        
        request
        
    type Client(secrets: Secrets) =
        
        let sender = new HttpClient()
        
        member _.CreateImage(prompt: string, variant: ImageType) =
            
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
                
            let body = {|
                ``type`` = "image_gen"
                operation = "simple_compose"
                prompt = prompt.Trim()
                n_variants = 2
                width = width
                height = height
                n_frames = 1
                inpaint_items = []
            |}
            
            let request =
                ("https://sora.chatgpt.com/backend/video_gen", body |> JsonSerializer.serialize)
                |> RequestPayload.PostJson
                |> Helpers.toHttpRequestMessage
            
            sentinelToken ()
            |> Async.map (fun sentinelToken -> addHeaders secrets sentinelToken request)
            |> Async.bind (sender.SendAsync >> Async.AwaitTask)
            |> Async.bind Helpers.ensureSuccess
            |> Async.bind Helpers.toRecord<GenerateResponse>
