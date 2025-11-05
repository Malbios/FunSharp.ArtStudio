namespace FunSharp.ArtStudio.Model

open System

type SoraResult = {
    Id: Guid
    Timestamp: DateTimeOffset
    Task: SoraTask
    Images: string array
}

[<RequireQualifiedAccess>]
module SoraResult =
    
    let keyOf result =
        
        SoraTask.keyOf result.Task
