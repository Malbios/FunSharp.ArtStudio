namespace FunSharp.DeviantArt.Api

open System
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open Suave
open Suave.Successful
open Suave.Operators
open Suave.Filters
open Suave.RequestErrors

[<RequireQualifiedAccess>]
module Http =

    type CallbackConfiguration = {
        Address: string
        Port: int
        Endpoint: string
    }
    
    type Request =
        | Get of url: string
        | PostWithFormContent of url: string * content: FormUrlEncodedContent
        | PostWithMultipartContent of url: string * content: MultipartFormDataContent

    let private client = new HttpClient()
    
    let formContent (values: Map<string, string>) =
        
        new FormUrlEncodedContent(values)

    let updateAuthorization accessToken =
        
        client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", accessToken)

    let request (payload: Request) =
            
        let request () = async {
            let! response =
                match payload with
                | Get url -> client.GetAsync url |> Async.AwaitTask
                | PostWithFormContent (url, content) -> client.PostAsync(url, content) |> Async.AwaitTask
                | PostWithMultipartContent (url, content) -> client.PostAsync(url, content) |> Async.AwaitTask
                
            let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            
            return (response, content)
        }
        
        let processResponse (response: HttpResponseMessage, content: string) =
            response.EnsureSuccessStatusCode() |> ignore
            content
        
        request ()
        |> Async.bind (fun (response, content) ->
            if response.StatusCode = HttpStatusCode.TooManyRequests then
                printfn "  Too many requests - waiting 30s..." 
                System.Threading.Thread.Sleep 30000
                
                request ()
                |> Async.map processResponse
            else
                processResponse (response, content) |> Async.returnM
        )
        
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
