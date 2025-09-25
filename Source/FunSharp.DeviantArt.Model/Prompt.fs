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

    let identifier p1 p2 =
        
        keyOf p1 = keyOf p2
