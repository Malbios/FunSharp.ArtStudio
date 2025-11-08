namespace FunSharp.ArtStudio.Model

open System

type ChatGPTTask = {
    Id: Guid
    Timestamp: DateTimeOffset
    Inspiration: Inspiration
}

[<RequireQualifiedAccess>]
module ChatGPTTask =
    
    let keyOf task =
        
        task.Id
