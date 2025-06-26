namespace DeviantArt.Api.Client

open System.IO
open Newtonsoft.Json

[<RequireQualifiedAccess>]
module Secrets =
    
    type Secrets = {
        client_id: string
        client_secret: string
    }
    
    let filePath = ".secrets"
    
    let load () =
        
        File.ReadAllText filePath
        |> JsonConvert.DeserializeObject<Secrets>
