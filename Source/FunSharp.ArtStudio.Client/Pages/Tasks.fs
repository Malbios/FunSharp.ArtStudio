namespace FunSharp.ArtStudio.Client.Pages

open Bolero
open Bolero.Html
open FunSharp.ArtStudio.Model
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.ArtStudio.Client.Model
open FunSharp.ArtStudio.Client.Components

type Tasks() =
    inherit ElmishComponent<State, Message>()
    
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
        
    let soraTaskDetails (task: SoraTask) =
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center
            "Gap" => "0.2rem"
            
            Inspiration.render task.Prompt.Inspiration
                            
            div { $"{task.Timestamp}" }
        }
            
    member _.TasksWidget<'T>(tasks: Loadable<StatefulItem<'T> array>, sortProp, errorDetails, taskDetails) =
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
            
    override _.CssScope = CssScopes.``FunSharp.ArtStudio.Client``
    
    override this.View model dispatch =
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            this.TasksWidget(model.ChatGPTTasks, _.Timestamp, chatGPTTaskErrorDetails, chatGPTTaskDetails)
            
            hr { attr.style "width: 100%;" }
            
            this.TasksWidget(model.SoraTasks, _.Timestamp, soraTaskErrorDetails, soraTaskDetails)
        }
        |> Page.render model dispatch
