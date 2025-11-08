namespace FunSharp.ArtStudio.Model

open System

type ChatGPTResult = {
    Id: Guid
    Timestamp: DateTimeOffset
    Task: ChatGPTTask
    Text: string
}

[<RequireQualifiedAccess>]
module ChatGPTResult =
    
    let keyOf result =
        
        result.Id
        
    let identifier cr1 cr2 =
        
        keyOf cr1 = keyOf cr2
