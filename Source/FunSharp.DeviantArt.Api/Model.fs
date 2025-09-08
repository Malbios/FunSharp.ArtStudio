namespace FunSharp.DeviantArt.Api.Model

open System
open FunSharp.Common
open Newtonsoft.Json

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
    
    let empty = {
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
    [<JsonProperty("title")>]
    Title: string
    
    [<JsonProperty("noai")>]
    NoAi: bool
    
    [<JsonProperty("is_ai_generated")>]
    IsAiGenerated: bool
    
    [<JsonProperty("is_dirty")>]
    IsDirty: bool
}

[<RequireQualifiedAccess>]
module StashSubmission =
    
    let defaults = {
        Title = ""
        NoAi = false
        IsAiGenerated = true
        IsDirty = false
    }
            
    let toProperties (submission: StashSubmission) =
        submission
        |> Record.toKeyValueTypes
        |> KeyValueType.splitArrays

type MatureLevel =
    | Strict
    | Moderate
    
type MatureClassification =
    | Nudity
    | Sexual
    | Gore
    | Language
    | Ideology

type LicenseOptionsModify =
    | Yes
    | No
    | Share
    
type LicenseOptions = {
    [<JsonProperty("creative_commons")>]
    CreativeCommons: bool
    
    [<JsonProperty("commercial")>]
    Commercial: bool
    
    [<JsonProperty("modify")>]
    Modify: string
}

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

type PublishSubmission = {
    [<JsonProperty("is_mature")>]
    IsMature: bool
    
    // [<JsonProperty("mature_level")>]
    // MatureLevel: MatureLevel
    //
    // [<JsonProperty("mature_classification")>]
    // MatureClassification: MatureClassification array
    
    [<JsonProperty("feature")>]
    Feature: bool
    
    [<JsonProperty("allow_comments")>]
    AllowComments: bool
    
    [<JsonProperty("display_resolution")>]
    DisplayResolution: int
    
    [<JsonProperty("license_options")>]
    LicenseOptions: LicenseOptions
    
    [<JsonProperty("galleryids")>]
    Galleries: string array
    
    [<JsonProperty("allow_free_download")>]
    AllowFreeDownload: bool
    
    [<JsonProperty("add_watermark")>]
    AddWatermark: bool
    
    [<JsonProperty("tags")>]
    Tags: string array
    
    // [<JsonProperty("subject_tags")>]
    // SubjectTags: string array
    //
    // [<JsonProperty("subject_tag_types")>]
    // SubjectTagTypes: string array
    
    // [<JsonProperty("location_tag")>]
    // LocationTag: string
    
    // [<JsonProperty("groups")>]
    // Groups: string array
    //
    // [<JsonProperty("group_folders")>]
    // GroupFolders: string array
    
    [<JsonProperty("is_ai_generated")>]
    IsAiGenerated: bool
    
    [<JsonProperty("noai")>]
    NoAi: bool
    
    [<JsonProperty("itemid")>]
    ItemId: int64
}

[<RequireQualifiedAccess>]
module PublishSubmission =
    
    let private modifyToString modify =
        match modify with
        | Yes -> "yes"
        | No -> "no"
        | Share -> "share"
    
    let defaults = {
        IsMature = false
        Feature = false
        AllowComments = true
        DisplayResolution = DisplayResolution.Original |> int
        LicenseOptions = { CreativeCommons = true; Commercial = true; Modify = (modifyToString LicenseOptionsModify.Share) }
        Galleries = Array.empty
        AllowFreeDownload = true
        AddWatermark = false
        Tags = [ "digital_art"; "made_with_ai" ] |> Array.ofList
        IsAiGenerated = true
        NoAi = false
        ItemId = -1L
    }
            
    let toProperties (publication: PublishSubmission) =
        publication
        |> Record.toKeyValueTypes
        |> KeyValueType.splitArrays
        
type TokenResponse = {
    access_token: string
    token_type: string
    expires_in: int
    refresh_token: string
    scope: string
    status: string
}

type WhoAmIResponse = {
    [<JsonProperty("userid")>]
    id: string
    
    [<JsonProperty("usericon")>]
    icon: Uri

    [<JsonProperty("type")>]
    account_type: string

    username: string
}

type DeviationResponse = {
    [<JsonProperty("deviationid")>]
    id: string
    
    title: string
}

type GalleryResponse = {
    has_more: bool
    next_offset: int option
    results: DeviationResponse array
}

type MetadataResponse = {
    description: string
    stats: Stats option
}

type DeviationWithMetadata =
    DeviationResponse * MetadataResponse
    
type StashSubmissionResponse = {
    [<JsonProperty("itemid")>]
    item_id: int64
    
    [<JsonProperty("stackid")>]
    stack_id: int64
    
    status: string
    stack: string
}

type PublicationResponse = {
    [<JsonProperty("deviationid")>]
    id: string
    
    status: string
    url: string
}

type Gallery = {
    id: string
    name: string
}

type AuthenticationData = {
    AccessToken: string
    RefreshToken: string
}

[<RequireQualifiedAccess>]
module AuthenticationData =
    
    let fromTokenResponse response = {
        AccessToken = response.access_token
        RefreshToken = response.refresh_token
    }
    
type Image(name: string, mimeType: string, content: byte array) =
    
    member val Name = name with get
    member val MimeType = mimeType with get
    member val Content = content with get
    
    member _.AsUrl() =
        match mimeType, content with
        | mime, content when mime = "" || content.Length = 0 -> ""
        | mime, content -> $"data:{mime};base64,{Convert.ToBase64String(content)}"
        
[<RequireQualifiedAccess>]
module Image =
    
    let empty =
        Image("", "", Array.empty)

type Inspiration = {
    Id: string
    Url: Uri
}

type Prompt = {
    Id: string
    Text: string
    Inspiration: Inspiration option
}

type Metadata = {
    Id: string
    Inspiration: Inspiration option
    Title: string
    Gallery: string
    IsMature: bool
}

[<RequireQualifiedAccess>]
module Metadata =
    
    let empty = {
        Id = ""
        Inspiration = None
        Title = ""
        Gallery = ""
        IsMature = false
    }

type LocalDeviation = Metadata

[<RequireQualifiedAccess>]
module LocalDeviation =
    
    let empty : LocalDeviation =
        Metadata.empty

type StashedDeviation = {
    StashId: int64
    Metadata: Metadata
}

type PublishedDeviation = {
    Url: Uri
    Metadata: Metadata
}
