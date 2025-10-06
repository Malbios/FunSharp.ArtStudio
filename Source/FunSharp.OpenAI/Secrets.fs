namespace FunSharp.OpenAI

open System.IO
open FunSharp.Common

type Secrets = {
    bearer_token: string
    cookies: string
}

[<RequireQualifiedAccess>]
module Secrets =
    
    let filePath = "secrets.json"
    
    let load () =
        
        File.ReadAllText filePath
        |> JsonSerializer.deserialize<Secrets>
