namespace FunSharp.ArtStudio.Server

open System.IO
open FunSharp.Common
open FunSharp.ArtStudio.Model

type Secrets = {
    client_id: string
    client_secret: string
    
    galleries: Gallery[]
    snippets: ClipboardSnippet[]
}

[<RequireQualifiedAccess>]
module Secrets =
    
    let filePath = "secrets.json"
    
    let load () =
        
        File.ReadAllText filePath
        |> JsonSerializer.deserialize<Secrets>
