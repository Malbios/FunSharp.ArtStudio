namespace FunSharp.DeviantArt.Server

open System.IO
open FunSharp.Data
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.RequestErrors
open Suave.Writers
open FunSharp.Common
open FunSharp.DeviantArt.Api

module Program =
    
    let secrets = Secrets.load ()
    let authPersistence = Persistence.AuthenticationPersistence()
    let dataPersistence = PickledPersistence("persistence.db")
    let client = Client(authPersistence, secrets.client_id, secrets.client_secret)
    
    let readAllBytesAsync (path: string) : Async<byte[]> = async {
        // Use async I/O and allow other readers/writers
        use fs = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    bufferSize = 64 * 1024,
                    options = FileOptions.Asynchronous)

        // If the file is very large, consider chunked processing instead
        let len =
            match fs.Length with
            | l when l <= int64 System.Int32.MaxValue -> int l
            | _ -> failwith "File too large for a single buffer"

        let buffer = Array.zeroCreate<byte> len
        let! _ = fs.ReadAsync(buffer, 0, buffer.Length) |> Async.AwaitTask
        return buffer
    }
        
    let handleStashing title file =
        
        let submission = {
            StashSubmission.empty with
                Title = title
        }
        
        printfn $"title: {title}"
        printfn $"mime: {file.mimeType}"
        printfn $"tempFilePath: {file.tempFilePath}"
        
        readAllBytesAsync file.tempFilePath
        |> Async.bind (fun content ->
            let httpFile : Http.File = {
                MediaType = Some file.mimeType
                Content = content
            }
            
            printfn "submitting..."
            
            client.SubmitToStash(submission, httpFile)
            |> AsyncResult.getOrFail
        )
    
    let jsonResponse data =
        data
        |> JsonSerializer.serialize
        |> OK
        >=> setHeader "Content-Type" "application/json"
        
    let allowCors : WebPart =
        setHeader "Access-Control-Allow-Origin" "*" 
        >=> setHeader "Access-Control-Allow-Headers" "Content-Type"
        >=> setHeader "Access-Control-Allow-Methods" "GET, POST, OPTIONS"
        
    let corsPreflight : WebPart =
        pathRegex ".*" >=> OPTIONS >=> allowCors >=> OK "CORS preflight"

    let usernameHandler : WebPart =
        fun ctx -> async {
            let! username =
                client.WhoAmI()
                |> AsyncResult.getOrFail
                |> Async.map _.username
                
            return! {| username = username |} |> jsonResponse <| ctx
        }
        
    let stashHandler : WebPart =
        fun ctx -> async {
            match ctx.request.files with
            | [] ->
                return! BAD_REQUEST "No files uploaded" ctx
                
            | [ file ] ->
                let title =
                    match ctx.request.fieldData "title" with
                    | Choice1Of2 v -> v
                    | Choice2Of2 v -> failwith $"{v}"
                    
                let! response = handleStashing title file
                
                return! response |> jsonResponse <| ctx
                
            | _ ->
                return! BAD_REQUEST "Multiple files not supported" ctx
        }

    [<EntryPoint>]
    let main _ =
        
        let config = {
            defaultConfig with
                bindings = [ HttpBinding.createSimple HTTP "0.0.0.0" 5123 ]
        }
        
        let apiRoot = "/api/v1"
        
        allowCors >=> choose [
            corsPreflight
            
            GET >=> path $"{apiRoot}/username" >=> usernameHandler
            
            POST >=> path $"{apiRoot}/stash" >=> stashHandler
            
            NOT_FOUND "Unknown route"
        ]
        |> startWebServer config
        
        0
