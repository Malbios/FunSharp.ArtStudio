namespace FunSharp.Http

open System
open System.IO

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
