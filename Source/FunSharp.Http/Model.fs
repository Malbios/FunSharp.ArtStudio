namespace FunSharp.Http

open System.IO

type File =
    | InMemory of content: byte array * mediaType: string option
    | Stream of content: Stream * mediaType: string option
    
[<RequireQualifiedAccess>]
type RequestPayload =
    | Get of url: string
    | PostJson of url: string * json: string
    | PostForm of url: string * properties: (string * string) list
    | PostMultipart of url: string * file: File * properties: (string * string) list
