namespace FunSharp.OpenAI.Model

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
        | Running = 0
        | Succeeded = 1
        | Failed = 2
    
    [<JsonConverter(typeof<JsonStringEnumConverter>)>]
    type ModerationStatus =
        | Passed = 0
        | Terminal = 1
    
    type ModerationResult = {
        ``type``: ModerationStatus
        code: string option
        is_output_rejection: bool
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
        progress_pct: float
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
