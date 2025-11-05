namespace FunSharp.ArtStudio.Client.Pages

open System
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
                            p {  $"result: {result.Task.Id}" }
                            p {  $"prompt: {result.Task.Prompt}" }
                            p {  $"error: {error}" }
                        }
                        
                    | StatefulItem.Default result ->
                        comp<RadzenStack> {
                            "Orientation" => Orientation.Horizontal
                            "JustifyContent" => JustifyContent.Center
                            "AlignItems" => AlignItems.Center
                            
                            comp<RadzenStack> {
                                "Orientation" => Orientation.Vertical
                                "JustifyContent" => JustifyContent.Center
                                "AlignItems" => AlignItems.Center
                                "Gap" => "0.5rem"
                                
                                match result.Task.Prompt.Inspiration with
                                | None -> Node.Empty ()
                                | Some inspiration ->
                                    comp<FunSharp.Blazor.Components.Image> {
                                        "ImageUrl" => inspiration.ImageUrl
                                        "ClickUrl" => Some inspiration.Url
                                    }
                                        
                                comp<RadzenStack> {
                                    "Orientation" => Orientation.Horizontal
                                    "Gap" => "0.2rem"
                                    
                                    for imagePath in result.Images do
                                        
                                        let imageUrl =
                                            imagePath
                                            |> String.split '/'
                                            |> List.last
                                            |> fun x -> $"http://127.0.0.1:5123/automated/{x}"
                                            |> Uri
                                            
                                        comp<FunSharp.Blazor.Components.Image> {
                                            "ImageUrl" => Some imageUrl
                                        }
                                }
                            }
                            
                            comp<RadzenStack> {
                                "Orientation" => Orientation.Vertical
                                "Gap" => "0.2rem"
                                
                                div { $"{result.Timestamp}" }
                                div { $"{result.Task.Prompt.Text}" }
                            }
                        }
                )
                |> Helpers.renderArray
                |> Deviations.render
        }
        |> Page.render model dispatch
