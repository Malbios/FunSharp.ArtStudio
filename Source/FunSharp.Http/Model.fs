namespace FunSharp.Http

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
