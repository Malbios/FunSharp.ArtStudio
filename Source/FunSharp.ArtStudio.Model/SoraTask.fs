namespace FunSharp.ArtStudio.Model

open System
open FunSharp.OpenAI.Api.Model.Sora

type SoraTask = {
    Id: Guid
    Timestamp: DateTimeOffset
    Prompt: Prompt
    AspectRatio: AspectRatio
    ExistingImages: Uri array
}

[<RequireQualifiedAccess>]
module SoraTask =
    
    let keyOf task =
        
        task.Id
        
    let identifier st1 st2 =
        
        keyOf st1 = keyOf st2
