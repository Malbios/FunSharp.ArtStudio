namespace FunSharp.DeviantArt.Api.Model

open System
open System.IO
open System.Text.Json.Serialization
open FunSharp.Common

type Stats = {
    views: int
    views_today: int
    favourites: int
    comments: int
    downloads: int
    downloads_today: int
}

[<RequireQualifiedAccess>]
module Stats =
    
    let defaults = {
        comments = -1
        downloads = -1
        downloads_today = -1
        favourites = -1
        views = -1
        views_today = -1
    }
    
type Deviation = {
    id: string
    title: string
    description: string
    stats: Stats
}

type SubmitDestination =
    | RootStack
    | Replace of id: int64
    | Stack of id: int64
    | StackWithName of name: string
    
type StashSubmission = {
    title: string
    noai: bool
    is_ai_generated: bool
    is_dirty: bool
}

[<RequireQualifiedAccess>]
module StashSubmission =
    
    let defaults = {
        title = ""
        noai = false
        is_ai_generated = true
        is_dirty = false
    }
    
    let toProperties (submission: StashSubmission) =
        submission
        |> Record.toKeyValueTypes
        |> KeyValueType.splitArrays
        
type DisplayResolution =
    | Original = 0
    | Width_400px = 1
    | Width_600px = 2
    | Width_800px = 3
    | Width_900px = 4
    | Width_1024px = 5
    | Width_1280px = 6
    | Width_1600px = 7
    | Width_1920px = 8

type LicenseOptionsModify =
    | Yes
    | No
    | Share
    
type LicenseOptions = {
    creative_commons: bool
    commercial: bool
    modify: LicenseOptionsModify
}

[<RequireQualifiedAccess>]
module LicenseOptions =
    
    let defaults = {
        creative_commons = true
        commercial = true
        modify = LicenseOptionsModify.Share
    }
    
    let toProperties (options: LicenseOptions) =
        
        [
            "license_options[creative_commons]", JsonSerializer.serialize options.creative_commons
            "license_options[commercial]", JsonSerializer.serialize options.commercial
            "license_options[modify]", (Union.toString options.modify).ToLower()
        ]

type PublishSubmission = {
    itemid: int64
    is_mature: bool
    feature: bool
    allow_comments: bool
    display_resolution: int
    license_options: LicenseOptions
    galleryids: string array
    allow_free_download: bool
    add_watermark: bool
    tags: string array
    is_ai_generated: bool
    noai: bool
}

[<RequireQualifiedAccess>]
module PublishSubmission =
    
    let defaults = {
        itemid = -1L
        is_mature = false
        feature = false
        allow_comments = true
        display_resolution = DisplayResolution.Original |> int
        license_options = LicenseOptions.defaults
        galleryids = Array.empty
        allow_free_download = true
        add_watermark = false
        tags = [ "digital_art"; "made_with_ai" ] |> Array.ofList
        is_ai_generated = true
        noai = false
    }
    
    let toProperties (submission: PublishSubmission) =
        submission
        |> Record.toKeyValueTypes
        |> KeyValueType.splitArrays
        |> List.collect (fun (key, value) ->
            if key = "license_options" then
                LicenseOptions.toProperties submission.license_options
            else
                List.singleton (key, value)
        )
        
type WhoAmIResponse = {
    userid: string
    usericon: Uri
    username: string

    [<JsonPropertyName("type")>]
    account_type: string
}

type ImageResponse = {
    src: string
}

type DeviationResponse = {
    deviationid: string
    preview: ImageResponse
}

type GalleryDeviationResponse = {
    deviationid: string
    title: string
}

type GalleryResponse = {
    has_more: bool
    next_offset: int option
    results: GalleryDeviationResponse array
}

type MetadataResponse = {
    description: string
    stats: Stats option
}

type DeviationWithMetadata =
    GalleryDeviationResponse * MetadataResponse
    
type StashSubmissionResponse = {
    itemid: int64
    stackid: int64
    status: string
    stack: string
}

type PublishResponse = {
    deviationid: string
    status: string
    url: string
}

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
    
type InMemoryFile = {
    Content: byte array
    MediaType: string option
}

type FileStream = {
    Content: Stream
    MediaType: string option
}
    
type File =
    | InMemory of InMemoryFile
    | Stream of FileStream

[<RequireQualifiedAccess>]
type RequestPayload =
    | Get of url: string
    | PostForm of url: string * properties: (string * string) list
    | PostMultipart of url: string * file: File * properties: (string * string) list
