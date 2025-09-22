namespace FunSharp.DeviantArt.Server

open System.IO
open Newtonsoft.Json

type Gallery = {
    id: string
    name: string
}

type ClipboardSnippet = {
    label: string
    value: string
}

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
        |> JsonConvert.DeserializeObject<Secrets>
