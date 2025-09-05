namespace FunSharp.DeviantArt.Api

open System
open FunSharp.Common.Abstraction
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open FunSharp.Common
open FunSharp.Common.AsyncResultCE
open FunSharp.DeviantArt.Api.Model
    
module Endpoints =
    
    [<Literal>]
    let private common = "/api/v1/oauth2"
    
    let submitToStash =
        $"{common}/stash/submit"
        
    let publishFromStash =
        $"{common}/stash/publish"
    
    let whoAmI =
        $"{common}/user/whoami"
    
    let allDeviations limit offset =
        $"{common}/gallery/all?with_session=false&mature_content=true&limit={limit}&offset={offset}"
        
    let deviationMetadata query =
        $"{common}/deviation/metadata?{query}&ext_stats=true"
        
    let galleryFolders (limit: int) =
        $"{common}/gallery/folders?limit={limit}"
        
type Client(persistence: IAuthPersistence<AuthenticationData>, clientId: string, clientSecret: string) =

    let config: Authentication.Configuration = {
        RootUrl = "https://www.deviantart.com"
        ClientId = clientId
        ClientSecret = clientSecret
        RedirectUri = "http://localhost:8080/callback"
        Scope = "user browse stash publish"
        Callback = {
            Endpoint = "/callback"
            Address = "127.0.0.1"
            Port = 8080
        }
    }

    let updateAuthorization accessToken =
        
        Http.updateAuthorization accessToken
        
    let saveAuthDataAndUpdateAuthorization result =
        
        result
        |> AsyncResult.map JsonConvert.DeserializeObject<TokenResponse>
        |> AsyncResult.map AuthenticationData.fromTokenResponse
        |> AsyncResult.tee (fun data -> data |> persistence.Save)
        |> AsyncResult.tee (fun data -> updateAuthorization data.AccessToken)
    
    let getNewAuthData () =
        
        Authentication.authenticate config
        |> saveAuthDataAndUpdateAuthorization

    let authenticationData =
        
        persistence.Load()
        |> Option.map (fun data ->
            printfn "Re-using persisted authentication data..."
            
            updateAuthorization data.AccessToken
            
            data |> AsyncResult.returnM
        )
        |> Option.defaultWith (fun () ->
            printfn "Acquiring new token..."
            
            getNewAuthData ()
        )
    
    let refreshToken () =
        
        authenticationData
        |> AsyncResult.tee (fun _ -> printfn "Refreshing token...")
        |> AsyncResult.bind (fun data ->
            Authentication.refresh config data.RefreshToken
            |> saveAuthDataAndUpdateAuthorization
        )
        |> AsyncResult.ignore

    let request (payload: Http.RequestPayload) =
        
        fun () -> getNewAuthData () |> AsyncResult.ignore
        |> Http.requestWithRefreshAndReAuth payload refreshToken
        
    let toGalleryResponse content : GalleryResponse =
        
        let jsonObject = JObject.Parse content

        let results =
            jsonObject["results"] :?> JArray
            |> Seq.map (fun j -> JsonConvert.DeserializeObject<DeviationResponse>(j.ToString()))
            |> Seq.toArray

        let hasMore = jsonObject["has_more"].ToObject<bool>()

        let nextOffset =
            match jsonObject.TryGetValue "next_offset" with
            | true, value when value.Type <> JTokenType.Null -> Some(value.ToObject<int>())
            | _ -> None
            
        {
            has_more = hasMore
            next_offset = nextOffset
            results = results
        }

    let galleryPage (offset: int) =

        printfn $"Reading gallery offset {offset}..."

        $"{config.RootUrl}{Endpoints.allDeviations 24 offset}"
        |> Http.RequestPayload.Get
        |> request
        |> AsyncResult.map toGalleryResponse
        
    let toDeviations content =
        
        let jsonObject = JObject.Parse content

        jsonObject["metadata"] :?> JArray
        |> Seq.map (fun j ->
            let description = j["description"].Value<string>()
            let stats = JsonConvert.DeserializeObject<Stats>(j["stats"].ToString())
            
            let metadata : MetadataResponse = {
                description = description
                stats = Some stats
            }
            
            metadata
        )
        |> Seq.toArray

    let metadata chunkIndex (ids: string list) =

        printfn $"Reading metadata chunk {chunkIndex}..."

        ids
        |> Seq.map (fun id -> $"deviationids[]={Uri.EscapeDataString id}")
        |> String.concat "&"
        |> Endpoints.deviationMetadata
        |> fun endpoint -> Http.RequestPayload.Get $"{config.RootUrl}{endpoint}"
        |> request
        |> AsyncResult.map toDeviations
        
    let submitToStash (destination: SubmitDestination) (file: Http.File) (submission: StashSubmission) =
            
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
        
        let url = $"{config.RootUrl}{Endpoints.submitToStash}"
        
        Http.RequestPayload.PostWithFileAndProperties (url, file, properties)
        |> request
        |> AsyncResult.map JsonConvert.DeserializeObject<StashSubmissionResponse>

    member _.WhoAmI() =

        Endpoints.whoAmI
        |> fun endpoint -> Http.RequestPayload.Get $"{config.RootUrl}{endpoint}"
        |> request
        |> AsyncResult.map JsonConvert.DeserializeObject<WhoAmIResponse>
        
    member _.AllDeviations() =
            
        let rec loop offset acc = asyncResult {
            let! page = galleryPage offset
            let combined = acc @ Array.toList page.results

            match page.has_more, page.next_offset with
            | true, Some nextOffset -> return! loop nextOffset combined
            | _ -> return combined
        }

        loop 0 []

    member this.AllDeviationsWithMetadata() =
        
        this.AllDeviations ()
        |> AsyncResult.bind (fun deviations ->
            deviations
            |> List.chunkBySize 10
            |> List.indexed
            |> List.map (fun (i, deviationsChunk) ->
                deviationsChunk
                |> List.map _.id
                |> metadata i
            )
            |> AsyncResult.sequential
            |> AsyncResult.map (fun metadataChunks ->
                printfn "combining chunks..."
                metadataChunks
                |> Array.concat
                |> Array.toList
                |> List.zip deviations
            )
        )
        
    member _.SubmitToStash(submission: StashSubmission, file: Http.File) =
        
        submitToStash SubmitDestination.RootStack file submission
        
    member _.SubmitToStash(submission: StashSubmission, file: Http.File, stackId: int64) =
        
        submitToStash (SubmitDestination.Stack stackId) file submission
        
    member _.SubmitToStash(submission: StashSubmission, file: Http.File, stackName: string) =
        
        submitToStash (SubmitDestination.StackWithName stackName) file submission
        
    member _.ReplaceInStash(submission: StashSubmission, file: Http.File, id: int64) =
        
        submitToStash (SubmitDestination.Replace id) file submission
        
    member _.PublishFromStash(publication: StashPublication)  =
        
        let properties = publication |> StashPublication.toProperties
        
        ($"{config.RootUrl}{Endpoints.publishFromStash}", properties)
        |> Http.RequestPayload.PostWithProperties
        |> request
        |> AsyncResult.map JsonConvert.DeserializeObject<PublicationResponse>

    member _.GalleryFolders() =
        
        $"{config.RootUrl}{Endpoints.galleryFolders 50}"
        |> Http.RequestPayload.Get
        |> request

    member _.GetLatestDeviation() =
        
        $"{config.RootUrl}{Endpoints.allDeviations 1 0}"
        |> Http.RequestPayload.Get
        |> request
        |> AsyncResult.map toGalleryResponse
        |> AsyncResult.bind (fun galleryResponse ->
            $"deviationids[]={Uri.EscapeDataString galleryResponse.results[0].id}"
            |> Endpoints.deviationMetadata
            |> fun endpoint -> $"{config.RootUrl}{endpoint}"
            |> Http.RequestPayload.Get
            |> request
        )
