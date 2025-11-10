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
            "Gap" => "1rem"
            
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
                            
                            Inspiration.render result.Task.Prompt.Inspiration
                            
                            div { $"result: {result.Id}" }
                            div { $"timestamp: {result.Timestamp}" }
                            div { $"prompt: {result.Task.Prompt.Text}" }
                            div { $"aspect ratio: {result.Task.AspectRatio}" }
                            div { $"error: {error}" }
                        }
                        
                    | StatefulItem.Default result ->
                        comp<RadzenStack> {
                            // TODO: make a border to make it easier to spot non-doubles
                            //attr.style (if result.Images.Length < 2 || result.Images.Length > 2 then "" else "")
                            
                            "Orientation" => Orientation.Vertical
                            "JustifyContent" => JustifyContent.Center
                            "AlignItems" => AlignItems.Center
                            "Gap" => "0.5rem"
                            
                            Inspiration.render result.Task.Prompt.Inspiration
                                
                            comp<RadzenStack> {
                                "Orientation" => Orientation.Horizontal
                                
                                Button.renderSimple "Retry" (fun () -> Message.RetrySoraResult result |> dispatch)
                                Button.renderSimple "Forget" (fun () -> Message.ForgetSoraResult result |> dispatch)
                            }
                            
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
                
            hr { attr.style "width: 100%;" }
            
            Tasks.render model.SoraTasks _.Timestamp Tasks.soraTaskErrorDetails Tasks.soraTaskDetails
        }
        |> Page.render model dispatch
