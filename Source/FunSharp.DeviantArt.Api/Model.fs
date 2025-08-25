namespace FunSharp.DeviantArt.Api

open System
open Newtonsoft.Json

module Model =

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

module ApiResponses =
    
    type Token = {
        access_token: string
        token_type: string
        expires_in: int
        refresh_token: string
        scope: string
        status: string
    }

    [<RequireQualifiedAccess>]
    module Token =

        let empty = {
            access_token = ""
            token_type = ""
            expires_in = -1
            refresh_token = ""
            scope = ""
            status = ""
        }

    type WhoAmI = {
        [<JsonProperty("userid")>]
        user_id: string
        
        [<JsonProperty("usericon")>]
        icon: Uri

        [<JsonProperty("type")>]
        account_type: string

        username: string
    }

    type Deviation = {
        [<JsonProperty("deviationid")>]
        id: string
        
        title: string
    }

    type GalleryAll = {
        has_more: bool
        next_offset: int option
        results: Deviation[]
    }

    type Metadata = {
        description: string
        stats: Model.Stats option
    }

    type DeviationWithMetadata =
        Deviation * Metadata
        
    type StashSubmission = {
        [<JsonProperty("itemid")>]
        item_id: int64
        
        [<JsonProperty("stackid")>]
        stack_id: int64
        
        status: string
        stack: string
    }
