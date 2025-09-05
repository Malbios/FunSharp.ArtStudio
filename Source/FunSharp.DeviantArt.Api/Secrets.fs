namespace FunSharp.DeviantArt.Api

open System.IO
open Newtonsoft.Json

type Gallery = {
    id: string
    name: string
}

type Secrets = {
    client_id: string
    client_secret: string
    
    galleries: Gallery[]
}

[<RequireQualifiedAccess>]
module Secrets =
    
    let filePath = "secrets.json"
    
    let load () =
        
        File.ReadAllText filePath
        |> JsonConvert.DeserializeObject<Secrets>
