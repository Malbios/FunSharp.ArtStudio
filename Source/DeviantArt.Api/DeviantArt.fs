namespace DeviantArt.Api

open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq

[<RequireQualifiedAccess>]
module DeviantArt =

    type TokenResponse = {
        access_token: string
        token_type: string
        expires_in: int
        refresh_token: string
        scope: string
        status: string
    }

    module TokenResponse =

        let empty = {
            access_token = String.Empty
            token_type = String.Empty
            expires_in = -1
            refresh_token = String.Empty
            scope = String.Empty
            status = String.Empty
        }

    type WhoAmI = {
        [<JsonProperty("userid")>]
        user_id: string

        username: string
        usericon: Uri

        [<JsonProperty("type")>]
        account_type: string
    }

    type Stats = {
        views: int
        views_today: int
        favourites: int
        comments: int
        downloads: int
        downloads_today: int
    }

    type Metadata = {
        description: string
        stats: Stats option
    }

    type Deviation = {
        [<JsonProperty("deviationid")>]
        id: string
        title: string
    }

    type GalleryAllResponse = {
        has_more: bool
        next_offset: int option
        results: Deviation[]
    }

    type DeviationWithMetadata = Deviation * Metadata

    type Client(persistence: IPersistence<TokenResponse>, clientId: string, clientSecret: string) =

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
            |> Async.map JsonConvert.DeserializeObject<TokenResponse>
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
            |> Async.map JsonConvert.DeserializeObject<TokenResponse>
            |> Async.tee persistence.Save
            |> Async.tee (fun tokenResponse -> updateAuthorization tokenResponse.access_token)
            |> Async.Ignore

        let request endpoint (content: Map<string, string> option) =
            
            let acquireNewToken () = acquireNewToken () |> Async.Ignore

            Http.requestWithRefreshAndReAuth $"{config.RootUrl}{endpoint}" content refreshToken acquireNewToken

        let galleryPage (offset: int) =

            let request endpoint =
                request endpoint None

            printfn $"Reading gallery offset {offset}..."

            $"/api/v1/oauth2/gallery/all?with_session=false&mature_content=true&limit=24&offset={offset}"
            |> request
            |> Async.map (fun content ->
                let jsonObject = JObject.Parse content

                let results =
                    jsonObject["results"] :?> JArray
                    |> Seq.map (fun j -> JsonConvert.DeserializeObject<Deviation>(j.ToString()))
                    |> Seq.toArray

                let hasMore = jsonObject["has_more"].ToObject<bool>()

                let nextOffset =
                    match jsonObject.TryGetValue "next_offset" with
                    | true, value when value.Type <> JTokenType.Null -> Some(value.ToObject<int>())
                    | _ -> None

                { has_more = hasMore
                  next_offset = nextOffset
                  results = results })

        let metadata chunkIndex (ids: string list) =

            let requestWithQuery query =
                request $"/api/v1/oauth2/deviation/metadata?{query}&ext_stats=true" None
                
            printfn $"Reading metadata chunk {chunkIndex}..."

            ids
            |> Seq.map (fun id -> $"deviationids[]={Uri.EscapeDataString id}")
            |> String.concat "&"
            |> requestWithQuery
            |> Async.map (fun content ->
                let jsonObject = JObject.Parse content

                jsonObject["metadata"] :?> JArray
                |> Seq.map (fun j ->
                    let description = j["description"].Value<string>()
                    let stats = JsonConvert.DeserializeObject<Stats>(j["stats"].ToString())
                    
                    {
                        description = description
                        stats = Some stats
                    }
                )
                |> Seq.toArray
            )

        member _.WhoAmI() =

            request "/api/v1/oauth2/user/whoami" None
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
                |> List.map (fun (i, chunk) ->
                    chunk
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
