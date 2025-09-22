namespace FunSharp.DeviantArt.Model

open System

type PublishedDeviation = {
    ImageUrl: Uri
    Timestamp: DateTimeOffset
    Url: Uri
    Origin: DeviationOrigin
    Metadata: Metadata
}

[<RequireQualifiedAccess>]
module PublishedDeviation =
    
    let keyOf deviation =
        
        deviation.ImageUrl
