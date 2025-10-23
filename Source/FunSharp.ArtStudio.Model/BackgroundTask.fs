namespace FunSharp.ArtStudio.Model

open FunSharp.OpenAI.Api.Model.Sora

type SoraGenerationTask = {
    Details: TaskDetails
    Deviation: LocalDeviation
}

[<RequireQualifiedAccess>]
module SoraTask =
    
    let keyOf task =
        
        task.Details.id

    let identifier t1 t2 =
        
        keyOf t1 = keyOf t2

type BackgroundTask =
    | SoraGeneration of SoraGenerationTask
    | NewInspiration of url: string
