namespace FunSharp.DeviantArt.Api

open System
open System.IO
open System.Net.Http
open System.Net.Http.Headers
open FunSharp.DeviantArt.Api.ApiResponses
open FunSharp.DeviantArt.Api.Model
open Newtonsoft.Json
open Newtonsoft.Json.Linq
    
module private Endpoints =
    
    [<Literal>]
    let private common = "/api/v1/oauth2"
    
    let submitToStash =
        $"{common}/stash/submit"
    
    let whoAmI =
        $"{common}/user/whoami"
    
    let allDeviations offset =
        $"{common}/gallery/all?with_session=false&mature_content=true&limit=24&offset={offset}"
        
    let deviationMetadata query =
        $"{common}/deviation/metadata?{query}&ext_stats=true"
        
type Client(persistence: IPersistence<Token>, clientId: string, clientSecret: string) =

    let config: Authentication.Configuration = {
        RootUrl = "https://www.deviantart.com"
        ClientId = clientId
        ClientSecret = clientSecret
        RedirectUri = "http://localhost:8080/callback"
        Scope = "user browse"
        Callback = {
            Endpoint = "/callback"
            Address = "127.0.0.1"
            Port = 8080
        }
    }

    let updateAuthorization accessToken =
        
        Http.updateAuthorization accessToken
    
    let acquireNewToken () =
        
        printfn "Acquiring new token..."
        
        Authentication.authenticate config
        |> Async.map JsonConvert.DeserializeObject<Token>
        |> Async.tee persistence.Save
        |> Async.tee (fun tokenResponse -> updateAuthorization tokenResponse.access_token)

    let token =

        match persistence.Load() with
        | None -> acquireNewToken () |> Async.RunSynchronously
        | Some tokenResponse ->
            printfn "Re-using persisted token..."
            
            updateAuthorization tokenResponse.access_token
            tokenResponse

    let refreshToken () =

        printfn "Refreshing token..."

        Authentication.refresh config token.refresh_token
        |> Async.map JsonConvert.DeserializeObject<Token>
        |> Async.tee persistence.Save
        |> Async.tee (fun tokenResponse -> updateAuthorization tokenResponse.access_token)
        |> Async.Ignore

    let request (payload: Http.Request) =
        
        fun () -> acquireNewToken () |> Async.Ignore
        |> Http.requestWithRefreshAndReAuth payload refreshToken

    let galleryPage (offset: int) : Async<GalleryAll> =

        printfn $"Reading gallery offset {offset}..."

        Endpoints.allDeviations offset
        |> fun endpoint -> Http.Request.Get $"{config.RootUrl}{endpoint}"
        |> request
        |> Async.map (fun content ->
            let jsonObject = JObject.Parse content

            let results =
                jsonObject["results"] :?> JArray
                |> Seq.map (fun j -> JsonConvert.DeserializeObject<ApiResponses.Deviation>(j.ToString()))
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
        )

    let metadata chunkIndex (ids: string list) =

        printfn $"Reading metadata chunk {chunkIndex}..."

        ids
        |> Seq.map (fun id -> $"deviationids[]={Uri.EscapeDataString id}")
        |> String.concat "&"
        |> Endpoints.deviationMetadata
        |> fun endpoint -> Http.Request.Get $"{config.RootUrl}{endpoint}"
        |> request
        |> Async.map (fun content ->
            let jsonObject = JObject.Parse content

            jsonObject["metadata"] :?> JArray
            |> Seq.map (fun j ->
                let description = j["description"].Value<string>()
                let stats = JsonConvert.DeserializeObject<Stats>(j["stats"].ToString())
            
                {
                    Metadata.description = description
                    stats = Some stats
                }
            )
            |> Seq.toArray
        )

    member _.WhoAmI() =

        Endpoints.whoAmI
        |> fun endpoint -> Http.Request.Get $"{config.RootUrl}{endpoint}"
        |> request
        |> Async.map JsonConvert.DeserializeObject<WhoAmI>
        
    member _.AllDeviations() =
            
        let rec loop offset acc = async {
            let! page = galleryPage offset
            let combined = acc @ Array.toList page.results

            match page.has_more, page.next_offset with
            | true, Some nextOffset -> return! loop nextOffset combined
            | _ -> return combined
        }

        loop 0 []

    member this.AllDeviationsWithMetadata() =
        
        this.AllDeviations ()
        |> Async.bind (fun deviations ->
            deviations
            |> List.chunkBySize 10
            |> List.indexed
            |> List.map (fun (i, deviationsChunk) ->
                deviationsChunk
                |> List.map _.id
                |> metadata i
            )
            |> Async.Sequential
            |> Async.map (fun metadataChunks ->
                printfn "combining chunks..."
                metadataChunks
                |> Array.concat
                |> Array.toList
                |> List.zip deviations))
        
    member _.SubmitToStash (filePath: string) =
        
        let title = Path.GetFileName(filePath)
        
        let content = new MultipartFormDataContent()
        
        content.Add(new StringContent(title), "title")
        
        let fileContent =
            File.ReadAllBytes filePath
            |> fun x -> new ByteArrayContent(x)
            
        fileContent.Headers.ContentType <- MediaTypeHeaderValue.Parse("application/octet-stream")
        
        content.Add(fileContent, "file", title)
        
        ($"{config.RootUrl}{Endpoints.submitToStash}", content)
        |> Http.Request.PostWithMultipartContent 
        |> request
        |> Async.tee (fun _ -> content.Dispose())
        |> Async.map JsonConvert.DeserializeObject<StashSubmission>
