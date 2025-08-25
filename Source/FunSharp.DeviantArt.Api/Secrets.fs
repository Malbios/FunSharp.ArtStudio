namespace FunSharp.DeviantArt.Api

open System.IO
open Newtonsoft.Json
    
type Secrets = {
    client_id: string
    client_secret: string
}

[<RequireQualifiedAccess>]
module Secrets =
    
    let filePath = ".secrets"
    
    let load () =
        
        File.ReadAllText filePath
        |> JsonConvert.DeserializeObject<Secrets>
