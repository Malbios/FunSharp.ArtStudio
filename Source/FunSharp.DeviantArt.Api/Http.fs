namespace FunSharp.DeviantArt.Api

open System
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
        Title: string
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
    type Request =
        | Get of url: string
        | PostWithProperties of url: string * properties: Map<string, string>
        | PostWithFileAndProperties of url: string * file: File * properties: Map<string, string>
        
    [<RequireQualifiedAccess>]
    type private InternalRequest =
        | Get of url: string
        | PostWithForm of url: string * content: FormUrlEncodedContent
        | PostWithMultipart of url: string * content: MultipartFormDataContent

    let private client = new HttpClient()

    let updateAuthorization accessToken =
        
        client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", accessToken)
        
    let private logResponse (response: HttpResponseMessage) content (payload: Request) =
        if Debug.isEnabled then
            [
                ""
                $"StatusCode: {response.StatusCode}"
                ""
                "Response Content:"
                content
                ""
                "Payload:"
                $"{payload}"
                ""
            ]
            |> String.concat "\n"
            |> fun x -> printfn $"{x}"
        
    let private apiError content =
        try
            JsonConvert.DeserializeObject<ApiError> content |> Some
        with
            | _ -> None
        
    let private internalRequest (payload: Request) =
        
        let internalRequest =
            match payload with
            | Request.Get url ->
                InternalRequest.Get url
            | Request.PostWithProperties (url, properties) ->
                InternalRequest.PostWithForm (url, new FormUrlEncodedContent(properties))
            | Request.PostWithFileAndProperties (url, file, properties) ->
                let fileContent = new ByteArrayContent(file.Content)
                let mediaType = match file.MediaType with | Some v -> v | None -> "application/octet-stream"
                
                fileContent.Headers.ContentType <- MediaTypeHeaderValue.Parse(mediaType)
                
                let content = new MultipartFormDataContent()
                content.Add(fileContent, "file", file.Title)
                
                for kvp in properties do
                    content.Add(new StringContent(kvp.Value), kvp.Key)
                
                InternalRequest.PostWithMultipart (url, content)
                
        match internalRequest with
        | InternalRequest.Get url ->
            client.GetAsync url |> Async.AwaitTask
        | InternalRequest.PostWithForm (url, content) ->
            client.PostAsync(url, content)
            |> Async.AwaitTask
            |> Async.tee (fun _ -> content.Dispose())
        | InternalRequest.PostWithMultipart (url, content) ->
            client.PostAsync(url, content)
            |> Async.AwaitTask
            |> Async.tee (fun _ -> content.Dispose())

    let request (payload: Request) =
        
        internalRequest payload
        |> Async.bind (fun response ->
            match response.StatusCode with
            | HttpStatusCode.TooManyRequests ->
                printfn $"DEBUG: {response.Headers}"
                printfn "  Too many requests - waiting 30s..."
                
                Task.Delay 30000
                |> Async.AwaitTask
                |> Async.bind (fun () -> internalRequest payload)
                
            | _ ->
                response |> Async.returnM
        )
        |> Async.bind (fun response ->
            response.Content.ReadAsStringAsync()
            |> Async.AwaitTask
            |> Async.map (fun content -> response, content)
        )
        |> Async.tee (fun (response, content) ->
            logResponse response content payload
            
            response.EnsureSuccessStatusCode |> ignore
            
            match apiError content with
            | Some error -> failwith $"{error}"
            | None -> ()
        )
        |> Async.map snd
        
    // let requestWithRefresh (payload: Request) (refresh: unit -> Async<unit>) : Async<Response> =
    //     request payload
    //     |> Async.catch
    //     |> Async.bind (function
    //         | Choice1Of2 res -> async.Return res
    //         | Choice2Of2 (:? HttpRequestException as ex) when ex.StatusCode = Nullable HttpStatusCode.Unauthorized ->
    //             printfn "requestWithRefresh: 401"
    //             refresh ()
    //             |> Async.bind (fun () -> request payload)
    //         | Choice2Of2 ex ->
    //             return raise ex
    //     )

    let requestWithRefresh (payload: Request) (refresh: unit -> Async<unit>) = async {
        try
            return! request payload
        with :? HttpRequestException as ex when ex.StatusCode = Nullable HttpStatusCode.Unauthorized ->
            printfn "requestWithRefresh: 401"
            refresh () |> Async.RunSynchronously
            return! request payload
    }
    
    let requestWithRefreshAndReAuth (payload: Request) (refresh: unit -> Async<unit>) (reAuthenticate: unit -> Async<unit>) = async {
        
        try
            return! requestWithRefresh payload refresh
        with :? HttpRequestException as ex when ex.StatusCode = Nullable HttpStatusCode.Unauthorized ->
            printfn "requestWithRefreshAndReAuth: 401"
            reAuthenticate () |> Async.RunSynchronously
            return! request payload
    }

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
