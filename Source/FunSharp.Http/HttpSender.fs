namespace FunSharp.Http

open System.Net.Http
open FunSharp.Common
open FunSharp.Http.Authentication

type HttpSender(httpClient: HttpClient, authentication: IAuthentication) =
    
    member this.Send(payload: RequestPayload) =
        
        let request = payload |> Helpers.toHttpRequestMessage
        
        authentication.EnsureAccessToken(httpClient)
        |> Async.bind (fun () -> httpClient.SendAsync(request) |> Async.AwaitTask)
