namespace FunSharp.ArtStudio.Utilities

open System.IO
open FunSharp.Common

type Secrets = {
    client_id: string
    client_secret: string
}

[<RequireQualifiedAccess>]
module Secrets =
    
    let filePath = "secrets.json"
    
    let load () =
        
        File.ReadAllText filePath
        |> JsonSerializer.deserialize<Secrets>
