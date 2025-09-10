namespace FunSharp.DeviantArt.Api

open System
open System.Collections.Generic
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open System.Threading.Tasks
open Newtonsoft.Json
open Suave
open Suave.Successful
open Suave.Operators
open Suave.Filters
open Suave.RequestErrors
open FunSharp.Common

[<RequireQualifiedAccess>]
module Http =

    type CallbackConfiguration = {
        Address: string
        Port: int
        Endpoint: string
    }
    
    type File = {
        Content: byte array
        MediaType: string option
    }
    
    type private ApiError = {
        [<JsonProperty("error")>]
        Error: string
        
        [<JsonProperty("error_description")>]
        Description: string
        
        [<JsonProperty("error_details")>]
        Details: (string * string) array
    }
        
    [<RequireQualifiedAccess>]
    type private InternalPayload =
        | Get of url: string
        | PostWithForm of url: string * content: FormUrlEncodedContent
        | PostWithMultipart of url: string * content: MultipartFormDataContent
    
    [<RequireQualifiedAccess>]
    type RequestPayload =
        | Get of url: string
        | PostWithProperties of url: string * properties: (string * string) list
        | PostWithFileAndProperties of url: string * file: File * properties: (string * string) list
        
    [<RequireQualifiedAccess>]
    module private RequestPayload =
            
        let toInternalPayload payload =
            
            match payload with
            | RequestPayload.Get url ->
                InternalPayload.Get url
                
            | RequestPayload.PostWithProperties (url, properties) ->
                let properties = properties |> Seq.map (fun (k, v) -> KeyValuePair(k, v))
                // printfn "FormUrlEncodedContent"
                // printfn $"{properties |> JsonSerializer.serialize}"
                InternalPayload.PostWithForm (url, new FormUrlEncodedContent(properties))
                
            | RequestPayload.PostWithFileAndProperties (url, file, properties) ->
                let fileContent = new ByteArrayContent(file.Content)
                let mediaType = match file.MediaType with | Some v -> v | None -> "application/octet-stream"
                
                fileContent.Headers.ContentType <- MediaTypeHeaderValue.Parse(mediaType)
                
                let title =
                    properties
                    |> List.tryFind (fun x -> fst x = "title")
                    |> Option.map snd
                    |> Option.defaultValue ""
                
                let content = new MultipartFormDataContent()
                content.Add(fileContent, "file", title)
                
                for kvp in properties do
                    content.Add(new StringContent(snd kvp), fst kvp)
                
                InternalPayload.PostWithMultipart (url, content)

    let private client =
        let handler = new HttpClientHandler()
        
        handler.AutomaticDecompression <- DecompressionMethods.All
        
        new HttpClient(handler)
        
    let updateAuthorization accessToken =
        
        client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", accessToken)
        
    let private apiError content =
        
        try
            JsonConvert.DeserializeObject<ApiError> content |> Some
        with
            | _ -> None
        
    let private internalRequest (request: RequestPayload) =
        
        request
        |> RequestPayload.toInternalPayload
        |> function
            | InternalPayload.Get url ->
                client.GetAsync url |> Async.AwaitTask
            | InternalPayload.PostWithForm (url, content) ->
                client.PostAsync(url, content)
                |> Async.AwaitTask
                |> Async.map (fun response ->
                    content.Dispose()
                    response
                )
            | InternalPayload.PostWithMultipart (url, content) ->
                client.PostAsync(url, content)
                |> Async.AwaitTask
                |> Async.map (fun response ->
                    content.Dispose()
                    response
                )
                
    let retryTooManyRequests payload (response: HttpResponseMessage) =
        
        match response.StatusCode with
        | HttpStatusCode.TooManyRequests ->
            // printfn $"DEBUG: {response.Headers}"
            // printfn "  Too many requests - waiting 30s..."
            
            Task.Delay 30000
            |> Async.AwaitTask
            |> Async.bind (fun () -> internalRequest payload)
        | _ ->
            response |> Async.returnM

    let request (payload: RequestPayload) =
        
        internalRequest payload
        |> Async.bind (retryTooManyRequests payload)
        |> Async.bind (fun response ->
            response.Content.ReadAsStringAsync()
            |> Async.AwaitTask
            |> Async.map (fun content -> response, content)
        )
        |> Async.map (fun (response, content) ->
            response.EnsureSuccessStatusCode() |> ignore
            
            if content.Trim() = "" then
                failwith "response content is empty"
            
            match apiError content with
            | Some error -> failwith $"{error}"
            | None -> ()
            
            (response, content)
        )
        |> Async.catch
        |> AsyncResult.map snd
        
    let onUnauthorized f (result: Result<_, exn>) =
        
        match result with
        | Error (:? HttpRequestException as ex) when ex.StatusCode = Nullable HttpStatusCode.Unauthorized ->
            f ()
        | _ ->
            result |> Async.returnM

    let private refreshAndRetryOnUnauthorized payload refresh result =
        
        result
        |> onUnauthorized (fun () ->
            refresh ()
            |> AsyncResult.bind (fun () -> request payload)
        )

    let requestWithRefresh payload refresh =
        
        request payload
        |> Async.bind (refreshAndRetryOnUnauthorized payload refresh)
        
    let private reAuthenticateAndRetryOnUnauthorized payload reAuthenticate result =
        
        result
        |> onUnauthorized (fun () ->
            reAuthenticate ()
            |> AsyncResult.bind (fun () -> request payload)
        )
        
    let requestWithRefreshAndReAuth payload refresh reAuthenticate =
        
        requestWithRefresh payload refresh
        |> Async.bind (reAuthenticateAndRetryOnUnauthorized payload reAuthenticate)

    let getAuthorizationCode (config: CallbackConfiguration) =

        let mutable authCode = None

        let webPart =
            path config.Endpoint
            >=> Http.request (fun r ->
                match r.queryParam "code" with
                | Choice1Of2 code ->
                    authCode <- Some code
                    OK "Authorization code received. You can close this window."
                | Choice2Of2 err -> BAD_REQUEST err
            )

        let server =
            startWebServerAsync
                { defaultConfig with
                    bindings = [ HttpBinding.createSimple HTTP config.Address config.Port ] }
                webPart

        Async.Start(server |> snd)

        while authCode.IsNone do
            System.Threading.Thread.Sleep 100

        authCode.Value
