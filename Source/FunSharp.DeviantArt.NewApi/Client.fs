namespace FunSharp.DeviantArt.NewApi

open System
open System.Diagnostics
open System.Net.Http
open System.Net.Http.Headers
open System.Threading
open Suave
open Suave.Successful
open Suave.Operators
open Suave.Filters
open Suave.RequestErrors
open FunSharp.Common
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.NewApi.Model

[<RequireQualifiedAccess>]
type RequestPayload =
    | Get of url: string
    | PostForm of url: string * properties: (string * string) list
    | PostMultipart of url: string * file: File * properties: (string * string) list

type Client(persistence: IPersistence, sender: HttpClient, clientId: string, clientSecret: string) =
    
    [<Literal>]
    let dbKey_AuthenticationData = "authenticationData"
    
    let mutable authData =
        persistence.Find<string, AuthenticationData>(dbKey_AuthenticationData, dbKey_AuthenticationData)
        
    let updateAuthData newAuthData =
        persistence.Upsert(dbKey_AuthenticationData, dbKey_AuthenticationData, newAuthData) |> ignore
        authData <- Some newAuthData
        
    let toHttpRequestMessage (payload: RequestPayload) =
        
        match payload with
        | RequestPayload.Get url ->
            new HttpRequestMessage(HttpMethod.Get, url)
            
        | RequestPayload.PostForm (url, properties) ->
            let content = new FormUrlEncodedContent(dict properties)
            new HttpRequestMessage(HttpMethod.Post, url, Content = content)
            
        | RequestPayload.PostMultipart (url, file, properties) ->
            let content = new MultipartFormDataContent()
            
            let title =
                properties
                |> List.tryFind (fun x -> fst x = "title")
                |> Option.map snd
                |> Option.defaultValue ""
            
            match file with
            | File file -> content.Add(new ByteArrayContent(file.Content), "file", title)
            | Stream file -> content.Add(new StreamContent(file.Content), "file", title)
            
            for k, v in properties do
                content.Add(new StringContent(v), k)
                
            new HttpRequestMessage(HttpMethod.Post, url, Content = content)
    
    member private this.RequestToken(parameters) =
        
        sender.PostAsync("https://www.deviantart.com/oauth2/token", new FormUrlEncodedContent(dict parameters))
        |> Async.AwaitTask
        |> Async.bind (fun response ->
            response.EnsureSuccessStatusCode() |> ignore
            
            response.Content.ReadAsStringAsync()
            |> Async.AwaitTask
            |> Async.map (fun content ->
                content
                |> JsonSerializer.deserialize<TokenResponse>
                |> AuthenticationData.fromTokenResponse
            )
        )
    
    member private this.RefreshToken(refreshToken) =
        this.RequestToken [
            "grant_type", "refresh_token"
            "client_id", clientId
            "client_secret", clientSecret
            "refresh_token", refreshToken
        ]
        |> Async.map (fun newAuthData ->
            updateAuthData newAuthData
            newAuthData
        )
            
    member private this.Authenticate(authCode: string) =
        
        this.RequestToken [
            "grant_type", "authorization_code"
            "client_id", clientId
            "client_secret", clientSecret
            "redirect_uri", "http://localhost:8080/callback"
            "code", authCode
        ]
        |> Async.map updateAuthData
    
    member private this.EnsureAccessToken() =
        
        match authData with
        | None ->
            failwith "Got no authentication data, authenticate first!"
        | Some v when v.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(-1) ->
            sender.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", v.AccessToken)
            Async.returnM ()
        | Some v ->
            this.RefreshToken(v.RefreshToken)
            |> Async.map (fun newAuthData ->
                sender.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", newAuthData.AccessToken)
            )
        
    member private this.Send(payload: RequestPayload) =
        
        let request = payload |> toHttpRequestMessage
        
        this.EnsureAccessToken()
        |> Async.bind (fun () -> sender.SendAsync(request) |> Async.AwaitTask)
        
    member this.StartInteractiveLogin() = async {
        let authorizeEndpoint = "https://www.deviantart.com/oauth2/authorize"
        let redirectUri = Uri.EscapeDataString "http://localhost:8080/callback"
        let scope = Uri.EscapeDataString "user browse stash publish"
        
        let authUrl = $"{authorizeEndpoint}?response_type=code&client_id={clientId}&redirect_uri={redirectUri}&scope={scope}"

        let psi = ProcessStartInfo(FileName = authUrl, UseShellExecute = true)
        Process.Start(psi) |> ignore

        let mutable codeReceived : string option = None

        let app =
            path "/callback" >=> request (fun r ->
                match r.queryParam "code" with
                | Choice1Of2 code ->
                    codeReceived <- Some code
                    OK "Authentication successful. You can close this tab."
                | Choice2Of2 err ->
                    BAD_REQUEST err
            )

        let cts = new CancellationTokenSource()

        let listening, server =
            startWebServerAsync { defaultConfig with bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 8085 ] } app

        let! _ = listening

        Async.Start(server, cts.Token)

        while codeReceived.IsNone do
            do! Async.Sleep 200

        cts.Cancel()

        match codeReceived with
        | None ->
            failwith "No auth code received"
        | Some code ->
            do! this.Authenticate(code)
    }
    
    member this.WhoAmI() =
        
        RequestPayload.Get $"https://www.deviantart.com/{Endpoints.whoAmI}"
        |> this.Send
        |> Async.bind (fun response ->
            response.EnsureSuccessStatusCode() |> ignore
            response.Content.ReadAsStringAsync() |> Async.AwaitTask
        )
        |> Async.map JsonSerializer.deserialize<WhoAmIResponse>
    
    interface IDisposable with
        
        member _.Dispose() =
            
            sender.Dispose()
