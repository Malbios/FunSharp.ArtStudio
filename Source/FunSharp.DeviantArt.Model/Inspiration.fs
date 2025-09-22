namespace FunSharp.DeviantArt.Model

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
