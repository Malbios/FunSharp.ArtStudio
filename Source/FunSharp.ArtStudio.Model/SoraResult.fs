namespace FunSharp.ArtStudio.Model

open System

type SoraResult = {
    Id: Guid
    Timestamp: DateTimeOffset
    Task: SoraTask
    Images: Uri array
}

[<RequireQualifiedAccess>]
module SoraResult =
    
    let keyOf result =
        
        SoraTask.keyOf result.Task
        
    let identifier sr1 sr2 =
        
        keyOf sr1 = keyOf sr2
