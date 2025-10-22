namespace FunSharp.DeviantArt.Api

open System
open System.Net.Http
open System.Text.RegularExpressions
open FunSharp.Http
open FunSharp.Common
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.Api.Model
open FunSharp.Http.Authentication

type Client(persistence: IPersistence, clientId: string, clientSecret: string) =
    
    [<Literal>]
    let tokenEndpoint = "https://www.deviantart.com/oauth2/token"
    
    [<Literal>]
    let redirectUrl = "http://localhost:8080/callback"
    
    let authentication = OAuthAuthentication(persistence, tokenEndpoint, redirectUrl, clientId, clientSecret) :> IAuthentication
    
    let httpClient = new HttpClient()
    let httpSender =  HttpSender(httpClient, authentication)
    
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
        |> httpSender.Send
        |> Async.bind Helpers.ensureSuccess
        |> Async.bind Helpers.toRecord<StashSubmissionResponse>
        
    member _.NeedsInteraction = authentication.NeedsInteraction
    
    member _.StartInteractiveLogin() =
        
        authentication.StartInteractiveLogin(httpClient)
    
    member this.DownloadFile(url: string) =
        
        authentication.EnsureAccessToken(httpClient)
        |> Async.bind (fun () ->
            url
            |> httpClient.GetByteArrayAsync
            |> Async.AwaitTask
        )
    
    member this.WhoAmI() =
        
        RequestPayload.Get Endpoints.whoAmI
        |> httpSender.Send
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
        |> httpSender.Send
        |> Async.bind Helpers.ensureSuccess
        |> Async.bind Helpers.toRecord<PublishResponse>
        
    member this.GetDeviationId(url: Uri) =
        
        url.ToString()
        |> RequestPayload.Get
        |> httpSender.Send
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
        |> httpSender.Send
        |> Async.bind Helpers.ensureSuccess
        |> Async.bind Helpers.toRecord<DeviationResponse>
    
    interface IDisposable with
        
        member _.Dispose() =
            
            httpClient.Dispose()
