namespace FunSharp.DeviantArt

open System
open System.IO
open Newtonsoft.Json

[<RequireQualifiedAccess>]
module Data =
    
    type Deviation = {
        FilePath: string
        Inspiration: Uri
        Title: string
        Gallery: string
        IsMature: bool
    }

    let readDeviations () =
        
        File.ReadAllText "data.json"
        |> JsonConvert.DeserializeObject<Deviation array>
