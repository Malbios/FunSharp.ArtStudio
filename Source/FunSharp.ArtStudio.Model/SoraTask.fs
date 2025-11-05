namespace FunSharp.ArtStudio.Model

open System
open FunSharp.OpenAI.Api.Model.Sora

type SoraTask = {
    Id: Guid
    Timestamp: DateTimeOffset
    Prompt: Prompt
    AspectRatio: AspectRatio
}

[<RequireQualifiedAccess>]
module SoraTask =
    
    let keyOf task =
        
        task.Id
