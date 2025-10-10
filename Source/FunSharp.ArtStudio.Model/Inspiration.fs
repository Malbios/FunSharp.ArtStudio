namespace FunSharp.ArtStudio.Model

open System

type Inspiration = {
    Url: Uri
    Timestamp: DateTimeOffset
    ImageUrl: Uri option
}

[<RequireQualifiedAccess>]
module Inspiration =
    
    let keyOf inspiration =
        
        inspiration.Url

    let identifier i1 i2 =
        
        keyOf i1 = keyOf i2
