namespace FunSharp.DeviantArt.Model

open System

type LocalDeviation = {
    ImageUrl: Uri
    Timestamp: DateTimeOffset
    Origin: DeviationOrigin
    Metadata: Metadata
}

[<RequireQualifiedAccess>]
module LocalDeviation =
    
    let defaults imageUrl = {
        ImageUrl = imageUrl
        Timestamp = DateTimeOffset.Now
        Origin = DeviationOrigin.None
        Metadata = Metadata.defaults
    }
    
    let keyOf deviation =
        
        deviation.ImageUrl
