namespace FunSharp.Http

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

module Authentication =
    
    type TokenResponse = {
        access_token: string
        token_type: string
        expires_in: int
        refresh_token: string
        scope: string
        status: string
    }

    type AuthenticationData = {
        AccessToken: string
        RefreshToken: string
        ExpiresAt: DateTimeOffset
    }

    [<RequireQualifiedAccess>]
    module AuthenticationData =
        
        let fromTokenResponse response = {
            AccessToken = response.access_token
            RefreshToken = response.refresh_token
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(response.expires_in)
        }
        
    type IAuthentication =
        
        abstract member NeedsInteraction: bool
        abstract member EnsureAccessToken : HttpClient -> Async<unit>
        abstract member StartInteractiveLogin : HttpClient -> Async<unit>

    type OAuthAuthentication(persistence: IPersistence, tokenEndpoint: string, redirectUrl: string, clientId: string, clientSecret: string) =
    
        [<Literal>]
        let dbKey_AuthenticationData = "authenticationData"
        
        let mutable authData =
            persistence.Find<string, AuthenticationData>(dbKey_AuthenticationData, dbKey_AuthenticationData)
            
        let updateAuthData newAuthData =
            persistence.Upsert(dbKey_AuthenticationData, dbKey_AuthenticationData, newAuthData) |> ignore
            authData <- Some newAuthData
        
        member private this.RequestToken(httpClient: HttpClient, parameters) : Async<AuthenticationData> =
            
            httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(dict parameters))
            |> Async.AwaitTask
            |> Async.bind Helpers.ensureSuccess
            |> Async.bind Helpers.toRecord<TokenResponse>
            |> Async.map AuthenticationData.fromTokenResponse
        
        member private this.RefreshToken(httpClient: HttpClient, refreshToken) =
            this.RequestToken(httpClient, [
                "grant_type", "refresh_token"
                "client_id", clientId
                "client_secret", clientSecret
                "refresh_token", refreshToken
            ])
            |> Async.map (fun newAuthData ->
                updateAuthData newAuthData
                newAuthData
            )
                
        member private this.Authenticate(httpClient: HttpClient, authCode: string) =
            
            this.RequestToken(httpClient, [
                "grant_type", "authorization_code"
                "client_id", clientId
                "client_secret", clientSecret
                "redirect_uri", redirectUrl
                "code", authCode
            ])
            |> Async.map updateAuthData
            
        interface IAuthentication with
            
            member _.NeedsInteraction = authData.IsNone
        
            member this.EnsureAccessToken(httpClient: HttpClient) =
                
                match authData with
                | None ->
                    failwith "Got no authentication data, authenticate first!"
                | Some v when v.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(-1) ->
                    httpClient.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", v.AccessToken)
                    Async.returnM ()
                | Some v ->
                    printfn "Refreshing access token..."
                    this.RefreshToken(httpClient, v.RefreshToken)
                    |> Async.map (fun newAuthData ->
                        httpClient.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", newAuthData.AccessToken)
                    )
            
            member this.StartInteractiveLogin(httpClient: HttpClient) = async {
                let authorizeEndpoint = "https://www.deviantart.com/oauth2/authorize"
                let redirectUri = Uri.EscapeDataString "http://localhost:8080/callback"
                let scope = Uri.EscapeDataString "user browse stash publish"
                
                let authUrl = $"{authorizeEndpoint}?response_type=code&client_id={clientId}&redirect_uri={redirectUri}&scope={scope}"

                let codeAsync =
                    Async.FromContinuations(fun (cont, _, _) ->
                        let app =
                            path "/callback" >=> request (fun r ->
                                match r.queryParam "code" with
                                | Choice1Of2 code ->
                                    fun ctx -> async {
                                        let! res = OK "Authentication successful. You can close this tab." ctx
                                        cont code
                                        return res
                                    }
                                | Choice2Of2 err ->
                                    BAD_REQUEST err
                            )

                        let cts = new CancellationTokenSource()
                        let config = { defaultConfig with bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 8080 ] }
                        let _, server = startWebServerAsync config app
                        Async.Start(server, cts.Token)

                        try
                            let psi = ProcessStartInfo(FileName = authUrl, UseShellExecute = true)
                            Process.Start(psi) |> ignore
                        with ex ->
                            cont (failwith $"Failed to open browser: {ex.Message}")
                    )

                let! code = codeAsync
                do! Async.Sleep 1000
                do! this.Authenticate(httpClient, code)
            }
