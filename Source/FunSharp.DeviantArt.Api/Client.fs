namespace FunSharp.DeviantArt.Api

open System
open System.Collections.Generic
open System.Diagnostics
open System.Net.Http
open System.Net.Http.Headers
open System.Text.RegularExpressions
open System.Threading
open Suave
open Suave.Successful
open Suave.Operators
open Suave.Filters
open Suave.RequestErrors
open FunSharp.Common
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.Api.Model

[<RequireQualifiedAccess>]
module Helpers =
        
    let toHttpRequestMessage (payload: RequestPayload) =
        
        match payload with
        | RequestPayload.Get url ->
            new HttpRequestMessage(HttpMethod.Get, url)
            
        | RequestPayload.PostForm (url, properties) ->
            let properties = properties |> Seq.map (fun (k, v) -> KeyValuePair(k, v))
            let content = new FormUrlEncodedContent(properties)
            new HttpRequestMessage(HttpMethod.Post, url, Content = content)
            
        | RequestPayload.PostMultipart (url, file, properties) ->
            let content = new MultipartFormDataContent()
            
            let title =
                properties
                |> List.tryFind (fun (k, _) -> k = "title")
                |> Option.map snd
                |> Option.defaultValue ""
                
            let mediaType =
                match file with
                | InMemory f -> f.MediaType
                | Stream f   -> f.MediaType
                |> Option.defaultValue "application/octet-stream"
                
            let fileContent =
                match file with
                | InMemory f -> new ByteArrayContent(f.Content) :> System.Net.Http.HttpContent
                | Stream f   -> new StreamContent(f.Content)   :> System.Net.Http.HttpContent
                
            fileContent.Headers.ContentType <- MediaTypeHeaderValue.Parse(mediaType)
            content.Add(fileContent, "file", title)
            
            for k, v in properties do
                content.Add(new StringContent(v), k)
                
            new HttpRequestMessage(HttpMethod.Post, url, Content = content)
            
    let ensureSuccess (response: HttpResponseMessage) =
        
        if response.IsSuccessStatusCode then
            response |> Async.returnM
        else
            response.Content.ReadAsStringAsync()
            |> Async.AwaitTask
            |> Async.map failwith
        
    let responseContentAsString (response: HttpResponseMessage) =
        
        response.Content.ReadAsStringAsync()
        |> Async.AwaitTask
    
    let toRecord<'T> (response: HttpResponseMessage) =
        response
        |> responseContentAsString
        |> Async.map JsonSerializer.deserialize<'T>

type Client(persistence: IPersistence, sender: HttpClient, clientId: string, clientSecret: string) =
    
    [<Literal>]
    let dbKey_AuthenticationData = "authenticationData"
    
    let mutable authData =
        persistence.Find<string, AuthenticationData>(dbKey_AuthenticationData, dbKey_AuthenticationData)
        
    let updateAuthData newAuthData =
        persistence.Upsert(dbKey_AuthenticationData, dbKey_AuthenticationData, newAuthData) |> ignore
        authData <- Some newAuthData
    
    member private this.RequestToken(parameters) : Async<AuthenticationData> =
        
        sender.PostAsync("https://www.deviantart.com/oauth2/token", new FormUrlEncodedContent(dict parameters))
        |> Async.AwaitTask
        |> Async.bind Helpers.ensureSuccess
        |> Async.bind Helpers.toRecord<TokenResponse>
        |> Async.map AuthenticationData.fromTokenResponse
    
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
            printfn "Refreshing access token..."
            this.RefreshToken(v.RefreshToken)
            |> Async.map (fun newAuthData ->
                sender.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", newAuthData.AccessToken)
            )
        
    member private this.Send(payload: RequestPayload) =
        
        let request = payload |> Helpers.toHttpRequestMessage
        
        this.EnsureAccessToken()
        |> Async.bind (fun () -> sender.SendAsync(request) |> Async.AwaitTask)
        
    member private this.SubmitToStash(destination: SubmitDestination, file: File, submission: StashSubmission) =
        
        let stack =
            match destination with
            | RootStack -> []
            | Replace id -> [ "itemid", $"{id}" ]
            | Stack id -> [ "stackid", $"{id}" ]
            | StackWithName name -> [ "stack", name ]
            
        let properties =
            submission
            |> StashSubmission.toProperties
            |> List.map (fun (key, value) ->
                let value =
                    if key = "title" then
                        value |> String.truncate 50
                    else
                        value
                        
                (key, value)
            )
            |> fun p -> p @ stack
        
        (Endpoints.submitToStash, file, properties)
        |> RequestPayload.PostMultipart
        |> this.Send
        |> Async.bind Helpers.ensureSuccess
        |> Async.bind Helpers.toRecord<StashSubmissionResponse>
        
    member _.NeedsInteraction = authData.IsNone
    
    member this.StartInteractiveLogin() = async {
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
        do! this.Authenticate(code)
    }
        
    member this.DownloadFile(url: string) =
        
        this.EnsureAccessToken()
        |> Async.bind (fun () ->
            url
            |> sender.GetByteArrayAsync
            |> Async.AwaitTask
        )
    
    member this.WhoAmI() =
        
        RequestPayload.Get Endpoints.whoAmI
        |> this.Send
        |> Async.bind Helpers.ensureSuccess
        |> Async.bind Helpers.toRecord<WhoAmIResponse>
        
    member this.SubmitToStash(submission: StashSubmission, file: File) =
        
        this.SubmitToStash(SubmitDestination.RootStack, file, submission)
        
    member this.SubmitToStash(submission: StashSubmission, file: File, stackId: int64) =
        
        this.SubmitToStash(SubmitDestination.Stack stackId, file, submission)
        
    member this.SubmitToStash(submission: StashSubmission, file: File, stackName: string) =
        
        this.SubmitToStash(SubmitDestination.StackWithName stackName, file, submission)
        
    member this.ReplaceInStash(submission: StashSubmission, file: File, id: int64) =
        
        this.SubmitToStash(SubmitDestination.Replace id, file, submission)
        
    member this.PublishFromStash(submission: PublishSubmission)  =
        
        let properties = submission |> PublishSubmission.toProperties
        
        ($"{Endpoints.publishFromStash}", properties)
        |> RequestPayload.PostForm
        |> this.Send
        |> Async.bind Helpers.ensureSuccess
        |> Async.bind Helpers.toRecord<PublishResponse>
        
    member this.GetDeviationId(url: Uri) =
        
        url.ToString()
        |> RequestPayload.Get
        |> this.Send
        |> Async.bind Helpers.ensureSuccess
        |> Async.bind Helpers.responseContentAsString
        |> Async.map (fun content ->
            let pattern = "<meta property=\"da:appurl\" content=\"DeviantArt://deviation/([0-9A-Fa-f\\-]+)\""
            let m = Regex.Match(content, pattern)
            
            if m.Success then
               m.Groups[1].Value
            else
                failwith "Could not find deviation UUID!"
        )

    member this.GetDeviation(id: string) =
        
        Endpoints.deviation id
        |> RequestPayload.Get
        |> this.Send
        |> Async.bind Helpers.ensureSuccess
        |> Async.bind Helpers.toRecord<DeviationResponse>
    
    interface IDisposable with
        
        member _.Dispose() =
            
            sender.Dispose()
