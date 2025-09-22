namespace FunSharp.DeviantArt.Model

open System

type StashedDeviation = {
    ImageUrl: Uri
    Timestamp: DateTimeOffset
    StashId: int64
    Origin: DeviationOrigin
    Metadata: Metadata
}

[<RequireQualifiedAccess>]
module StashedDeviation =
    
    let keyOf deviation =
        
        deviation.ImageUrl
