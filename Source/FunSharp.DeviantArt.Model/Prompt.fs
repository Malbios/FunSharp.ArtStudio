namespace FunSharp.DeviantArt.Model

open System
        
type Prompt = {
    Id: Guid
    Timestamp: DateTimeOffset
    Text: string
    Inspiration: Inspiration option
}

[<RequireQualifiedAccess>]
module Prompt =
    
    let keyOf prompt =
        
        prompt.Id
