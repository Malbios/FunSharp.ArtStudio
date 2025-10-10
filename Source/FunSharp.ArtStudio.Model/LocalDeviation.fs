namespace FunSharp.ArtStudio.Model

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

    let identifier d1 d2 =
        
        keyOf d1 = keyOf d2
