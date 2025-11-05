namespace FunSharp.ArtStudio.Client.Pages

open Bolero
open Bolero.Html
open FunSharp.ArtStudio.Model
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.ArtStudio.Client.Model
open FunSharp.ArtStudio.Client.Components

type Sora() =
    inherit ElmishComponent<State, Message>()
    
    let inspirationWidget (inspiration: Inspiration option) =
        
        match inspiration with
        | None -> Node.Empty ()
        | Some inspiration ->
            comp<RadzenStack> {
                "Orientation" => Orientation.Vertical
                "JustifyContent" => JustifyContent.Center
                "AlignItems" => AlignItems.Center
                "Gap" => "0.2rem"
                
                comp<FunSharp.Blazor.Components.Image> {
                    "ImageUrl" => inspiration.ImageUrl
                    "ClickUrl" => Some inspiration.Url
                }
                
                Link.renderSimple (Some "DA Link") inspiration.Url
            }
            
    let soraResultsWidget model dispatch =
        Loadable.render model.SoraResults
        <| fun results ->
            results
            |> StatefulItems.sortBy _.Timestamp
            |> Array.map (fun result ->
                match result with
                | StatefulItem.IsBusy _ ->
                    LoadingWidget.render ()
                    
                | StatefulItem.HasError (result, error) ->
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Vertical
                        "Gap" => "0.5rem"
                        
                        div { $"result: {result.Id}" }
                        div { $"timestamp: {result.Timestamp}" }
                        div { $"prompt: {result.Task.Prompt.Text}" }
                        div { $"aspect ratio: {result.Task.AspectRatio}" }
                        div { $"error: {error}" }
                    }
                    
                | StatefulItem.Default result ->
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Vertical
                        "JustifyContent" => JustifyContent.Center
                        "AlignItems" => AlignItems.Center
                        "Gap" => "0.5rem"
                        
                        inspirationWidget result.Task.Prompt.Inspiration
                            
                        Button.renderSimple "Retry" (fun () -> Message.RetrySoraResult result |> dispatch)
                        
                        comp<RadzenStack> {
                            "Orientation" => Orientation.Horizontal
                            "Gap" => "0.5rem"
                            
                            for i, imageUrl in result.Images |> Array.indexed do
                                
                                comp<RadzenStack> {
                                    "Orientation" => Orientation.Vertical
                                    "Gap" => "0.2rem"
                                    
                                    comp<FunSharp.Blazor.Components.Image> {
                                        "ImageUrl" => Some imageUrl
                                        "ClickUrl" => Some imageUrl
                                    }
                                    
                                    Button.renderSimple $"Pick {i + 1}" <| fun () ->
                                        Message.SoraResult2LocalDeviation (result, i) |> dispatch
                                }
                        }
                    }
            )
            |> Helpers.renderArray
            |> Deviations.render
            
    let soraTasksWidget model =
        Loadable.render model.SoraTasks
        <| fun tasks ->
            tasks
            |> StatefulItems.sortBy _.Timestamp
            |> Array.map (fun task ->
                match task with
                | StatefulItem.IsBusy _ ->
                    LoadingWidget.render ()
                    
                | StatefulItem.HasError (task, error) ->
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Vertical
                        "Gap" => "0.5rem"
                        
                        div { $"task: {task.Id}" }
                        div { $"timestamp: {task.Timestamp}" }
                        div { $"prompt: {task.Prompt.Text}" }
                        div { $"aspect ratio: {task.AspectRatio}" }
                        div { $"error: {error}" }
                    }
                    
                | StatefulItem.Default task ->
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Vertical
                        "JustifyContent" => JustifyContent.Center
                        "AlignItems" => AlignItems.Center
                        "Gap" => "0.2rem"
                        
                        inspirationWidget task.Prompt.Inspiration
                            
                        div { $"{task.Timestamp}" }
                    }
            )
            |> Helpers.renderArray
            |> Deviations.render
    
    override _.CssScope = CssScopes.``FunSharp.ArtStudio.Client``
    
    override this.View model dispatch =
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            soraResultsWidget model dispatch
            
            hr { attr.style "width: 100%;" }
            
            soraTasksWidget model
        }
        |> Page.render model dispatch
