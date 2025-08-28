namespace FunSharp.DeviantArt

open System
open System.IO
open Newtonsoft.Json

[<RequireQualifiedAccess>]
module Data =
    
    type DeviationMetadata = {
        Title: string
        Gallery: string
        IsMature: bool
        Inspiration: Uri
    }
    
    type LocalDeviation = {
        FilePath: string
        Metadata: DeviationMetadata
    }
    
    type StashedDeviation = {
        Id: int64
        Local: LocalDeviation
    }
    
    type PublishedDeviation = {
        Url: Uri
        Stashed: StashedDeviation
    }
    
    type Deviation =
        | Local of LocalDeviation
        | Stashed of StashedDeviation
        | Published of PublishedDeviation
    
    type State = {
        Deviations: Deviation array
    }

    let readLocalDeviations () =
        
        File.ReadAllText "data.json"
        |> JsonConvert.DeserializeObject<LocalDeviation array>
