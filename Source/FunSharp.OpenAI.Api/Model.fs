namespace FunSharp.OpenAI.Api.Model

open System.Text.Json.Serialization

module Sora =

    type ImageType =
        | Landscape
        | Square
        | Portrait
        
    type AuthenticationTokens = {
        Sentinel: string
        Cookies: string
        Bearer: string
    }

    [<RequireQualifiedAccess>]
    module AuthenticationTokens =
        
        let empty = {
            Sentinel = ""
            Cookies = ""
            Bearer = ""
        }

    type BearerToken = {
        accessToken: string
    }

    type Task = {
        id: string
    }

    type Error = {
        message: string
        ``type``: string
        code: string
    }

    type ErrorContainer = {
        error: Error
    }
    
    [<JsonConverter(typeof<JsonStringEnumConverter>)>]
    type TaskStatus =
        | PreProcessing = 0
        | Running = 1
        | Succeeded = 2
        | Failed = 3
        | Cancelled = 4
    
    [<JsonConverter(typeof<JsonStringEnumConverter>)>]
    type ModerationStatus =
        | Passed = 0
        | Terminal = 1
    
    type ModerationResult = {
        ``type``: ModerationStatus
        code: string option
        is_output_rejection: bool
    }

    [<RequireQualifiedAccess>]
    module ModerationResult =
        
        let empty = {
            ``type`` = ModerationStatus.Passed
            code = None
            is_output_rejection = false
        }
    
    type Generation = {
        id: string
        task_id: string
        created_at: string
        deleted_at: string option
        url: string
        seed: int
        can_download: bool
        download_status: string
        width: int
        height: int
        moderation_result: ModerationResult
        task_type: string
    }

    type TaskDetails = {
        id: string
        created_at: string
        status: TaskStatus
        progress_pct: float option
        ``type``: string
        prompt: string
        n_variants: int
        n_frames: int
        height: int
        width: int
        generations: Generation array
        num_unsafe_generations: int
        title: string
        moderation_result: ModerationResult
        failure_reason: string option
        needs_user_review: bool
    }

    [<RequireQualifiedAccess>]
    module TaskDetails =
        
        let empty = {
            id = ""
            created_at = ""
            status = TaskStatus.Failed
            progress_pct = None
            ``type`` = ""
            prompt = ""
            n_variants = 0
            n_frames = 0
            height = 0
            width = 0
            generations = Array.empty
            num_unsafe_generations = 0
            title = ""
            moderation_result = ModerationResult.empty
            failure_reason = None
            needs_user_review = false
        }
