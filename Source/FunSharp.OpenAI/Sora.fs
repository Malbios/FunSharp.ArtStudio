namespace FunSharp.OpenAI

open System
open System.Diagnostics
open System.Text
open FunSharp.Common

module Sora =
    
    type ImageType =
        | Landscape
        | Square
        | Portrait
        
    type AuthenticationTokens = {
        Sentinel: string
        Cookies: string
        Bearer: string
    }
    
    [<RequireQualifiedAccess>]
    module AuthenticationTokens =
        
        let empty = {
            Sentinel = ""
            Cookies = ""
            Bearer = ""
        }
    
    type BearerToken = {
        accessToken: string
    }
    
    type SoraTask = {
        id: string
    }
    
    type SoraError = {
        message: string
        ``type``: string
        code: string
    }
    
    type SoraErrorContainer = {
        error: SoraError
    }
    
    let private generateEndpoint = "https://sora.chatgpt.com/backend/video_gen"
    let private checkTaskEndpoint taskId = $"https://sora.chatgpt.com/backend/video_gen/{taskId}"
    
    let private runScript (scriptPath: string) (arguments: string array) =
        
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
        
        runScript @"C:\dev\fsharp\DeviantArt\Utilities\puppeteer\sentinel-and-cookies.js" Array.empty
        |> Async.map (fun output ->
            let lines = output.Trim().Split(Environment.NewLine)
            // printfn $"sentinel: {lines[0]}"
            // printfn $"cookies: {lines[1]}"
            (lines[0], lines[1])
        )
        
    let private runScript_BearerToken cookies =
        
        runScript @"C:\dev\fsharp\DeviantArt\Utilities\puppeteer\bearer.js" [|cookies|]
        |> Async.map (fun output ->
            // printfn $"bearer output: {output}"
            let bearer = JsonSerializer.deserialize<BearerToken> output
            // printfn $"bearer: {bearer.accessToken}"
            bearer.accessToken
        )
        
    let private runScript_CreateImage body (authTokens: AuthenticationTokens) =
        
        let args = [| authTokens.Sentinel; authTokens.Cookies; authTokens.Bearer; body |]
        runScript @"C:\dev\fsharp\DeviantArt\Utilities\puppeteer\create-image.js" args
        
    let deserializeResponse<'T> value =
        match JsonSerializer.tryDeserialize<'T> value, JsonSerializer.tryDeserialize<SoraErrorContainer> value with
        | Some object, _ -> object
        | None, Some error -> failwith $"{error.error.message}"
        | _ -> failwith $"could not deserialize this value: {value}"
        
    type Client() =
        
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
        
        member _.CreateImage(prompt: string, variant: ImageType) =
            
            let prompt = String.trim prompt
            
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
                
            let createImage body =
                runScript_CreateImage body authTokens
                
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
            |> createImage
            |> Async.map deserializeResponse<SoraTask>
            |> Async.map _.id
