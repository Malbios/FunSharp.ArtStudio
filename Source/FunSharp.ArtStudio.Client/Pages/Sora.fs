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
    
    override _.CssScope = CssScopes.``FunSharp.ArtStudio.Client``
    
    override this.View model dispatch =
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            Loadable.render model.SoraTasks
            <| fun tasks ->
                tasks
                |> StatefulItems.sortBy _.Timestamp
                |> Array.map (fun task ->
                    match task with
                    | StatefulItem.IsBusy _ ->
                        LoadingWidget.render ()
                        
                    | StatefulItem.HasError (task, error) ->
                        concat {
                            text $"task: {task.Id}"
                            text $"prompt: {task.Prompt}"
                            text $"error: {error}"
                        }
                        
                    | StatefulItem.Default task ->
                        concat {
                            text $"task: {task.Id}"
                            text $"timestamp: {task.Timestamp}"
                            text $"prompt: {task.Prompt}"
                            text $"aspect ratio: {task.AspectRatio}"
                        }
                )
                |> Helpers.renderArray
                |> Deviations.render
            
            Loadable.render model.SoraResults
            <| fun results ->
                results
                |> StatefulItems.sortBy _.Task.Timestamp
                |> Array.map (fun result ->
                    match result with
                    | StatefulItem.IsBusy _ ->
                        LoadingWidget.render ()
                        
                    | StatefulItem.HasError (result, error) ->
                        concat {
                            text $"result: {result.Task.Id}"
                            text $"prompt: {result.Task.Prompt}"
                            text $"error: {error}"
                        }
                        
                    | StatefulItem.Default result ->
                        comp<RadzenStack> {
                            "Orientation" => Orientation.Horizontal
                            
                            concat {
                                text $"result: {result.Task.Id}"
                                text $"timestamp: {result.Task.Timestamp}"
                                text $"prompt: {result.Task.Prompt}"
                                text $"aspect ratio: {result.Task.AspectRatio}"
                            }
                            
                            [ for image in result.Images do text image ]
                            |> Helpers.renderList
                        }
                )
                |> Helpers.renderArray
                |> Deviations.render
        }
        |> Page.render model dispatch
