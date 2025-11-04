namespace FunSharp.ArtStudio.Model
        
type SoraResult = {
    Task: SoraTask
    Images: string array
}

[<RequireQualifiedAccess>]
module SoraResult =
    
    let keyOf result =
        
        SoraTask.keyOf result.Task
