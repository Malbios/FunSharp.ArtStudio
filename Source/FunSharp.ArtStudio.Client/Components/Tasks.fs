namespace FunSharp.ArtStudio.Client.Components

open Bolero.Html
open FunSharp.ArtStudio.Client.Model
open FunSharp.ArtStudio.Model
open FunSharp.Blazor.Components
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Tasks =
    
    let chatGPTTaskErrorDetails (task: ChatGPTTask) (error: exn) =
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "0.5rem"
            
            Inspiration.render (Some task.Inspiration)
            
            div { $"task: {task.Id}" }
            div { $"timestamp: {task.Timestamp}" }
            div { $"error: {error}" }
        }
        
    let chatGPTTaskDetails (task: ChatGPTTask) =
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center
            "Gap" => "0.2rem"
            
            Inspiration.render (Some task.Inspiration)
                            
            div { $"{task.Timestamp}" }
        }
    
    let soraTaskErrorDetails (task: SoraTask) (error: exn) =
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "0.5rem"
            
            div { $"task: {task.Id}" }
            div { $"timestamp: {task.Timestamp}" }
            div { $"prompt: {task.Prompt.Text}" }
            div { $"aspect ratio: {task.AspectRatio}" }
            div { $"error: {error}" }
        }
        
    let soraTaskDetails dispatch (task: SoraTask) =
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center
            "Gap" => "0.2rem"
            
            Inspiration.render task.Prompt.Inspiration
                            
            div { $"{task.Timestamp}" }
            
            Button.renderSimple "Abort" (fun () -> Message.AbortSoraTask task |> dispatch)
        }
            
    let render<'T> (tasks: Loadable<StatefulItem<'T> array>) sortProp errorDetails taskDetails =
        Loadable.render tasks
        <| fun tasks ->
            tasks
            |> StatefulItems.sortBy sortProp 
            |> Array.map (fun task ->
                match task with
                | StatefulItem.IsBusy _ ->
                    LoadingWidget.render ()
                    
                | StatefulItem.HasError (task, error) ->
                    errorDetails task error
                    
                | StatefulItem.Default task ->
                    taskDetails task
            )
            |> Helpers.renderArray
            |> Deviations.render
