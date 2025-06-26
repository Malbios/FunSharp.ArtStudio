namespace DeviantArt.Api

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

    let private client = new HttpClient()

    let private mapToContent (values: Map<string, string>) =
        
        new FormUrlEncodedContent(values)

    let updateAuthorization accessToken =
        
        client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", accessToken)

    let request (url: string) (content: Map<string, string> option) =
            
        let request () = async {
            let! response =
                match content with
                | None -> client.GetAsync url |> Async.AwaitTask
                | Some body -> client.PostAsync(url, mapToContent body) |> Async.AwaitTask
                
            let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            
            return (response, content)
        }
        
        let processResponse (response: HttpResponseMessage, content: string) =
            response.EnsureSuccessStatusCode() |> ignore
                
            content
        
        request ()
        |> Async.bind (fun (response, content) ->
            if response.StatusCode = HttpStatusCode.TooManyRequests then
                System.Threading.Thread.Sleep 30000
                
                request ()
                |> Async.map processResponse
            else
                processResponse (response, content) |> Async.returnM
        )

    let requestWithRefresh (url: string) (content: Map<string, string> option) (refresh: unit -> Async<unit>) = async {
        
        try
            return! request url content
        with :? HttpRequestException as ex when ex.StatusCode = Nullable HttpStatusCode.Unauthorized ->
            printfn "requestWithRefresh: 401"
            refresh () |> Async.RunSynchronously
            return! request url content
    }
    
    let requestWithRefreshAndReAuth (url: string) (content: Map<string, string> option) (refresh: unit -> Async<unit>) (reAuth: unit -> Async<unit>) = async {
        
        try
            return! requestWithRefresh url content refresh
        with :? HttpRequestException as ex when ex.StatusCode = Nullable HttpStatusCode.Unauthorized ->
            printfn "requestWithRefreshAndReAuth: 401"
            reAuth () |> Async.RunSynchronously
            return! request url content
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
