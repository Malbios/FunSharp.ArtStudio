namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Model
open FunSharp.DeviantArt.Manager
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.Components

type StashedDeviations() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.StashedDeviations
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<_> with get, set
    
    [<Inject>]
    member val NavManager: NavigationManager = Unchecked.defaultof<_> with get, set
    
    override this.View model dispatch =
        
        let publish deviation =
            Message.PublishStashed deviation |> dispatch
        
        let stashedDeviationsCount =
            match model.StashedDeviations with
            | Loaded deviations -> deviations.Length
            | _ -> -1

        match stashedDeviationsCount with
        | 0 -> text "No items."
        | _ ->
            Loadable.render model.StashedDeviations
            <| fun deviations ->
                deviations
                |> StatefulItemArray.sortBy _.Timestamp
                |> Array.map (fun deviation ->
                    match deviation with
                    | IsBusy _ ->
                        LoadingWidget.render ()
                        
                    | HasError (deviation, error) ->
                        concat {
                            text $"deviation: {StashedDeviation.keyOf deviation}"
                            text $"error: {error}"
                        }
                        
                    | Default deviation ->
                        let inspirationUrl =
                            match deviation.Origin with
                            | DeviationOrigin.None -> None
                            | DeviationOrigin.Prompt prompt -> prompt.Inspiration |> Option.bind _.ImageUrl
                            | DeviationOrigin.Inspiration inspiration -> inspiration.ImageUrl
                                
                        concat {
                            deviation.Timestamp.ToString() |> text
                            
                            comp<RadzenStack> {
                                "Orientation" => Orientation.Horizontal
                                
                                comp<RadzenLink> {
                                    "Path" => $"{Helpers.stashEditUrl deviation.StashId}"
                                    "Text" => "Open in Sta.sh"
                                    "Target" => "_blank"
                                }

                                match deviation.Origin with
                                | DeviationOrigin.None -> ()
                                | DeviationOrigin.Inspiration inspiration ->
                                    Helpers.copyToClipboard this.JSRuntime $"Inspired by {inspiration.Url}"
                                    |> IconButton.render "Copy inspiration to clipboard"
                                | DeviationOrigin.Prompt prompt ->
                                    match prompt.Inspiration with
                                    | None -> ()
                                    | Some inspiration ->
                                        Helpers.copyToClipboard this.JSRuntime $"Inspired by {inspiration.Url}"
                                        |> IconButton.render "Copy inspiration to clipboard"
                            }
                            
                            Button.render "Publish" (fun () -> publish deviation) false
                        }
                        |> Deviation.renderWithContent inspirationUrl (Some deviation.ImageUrl)
                )
                |> Helpers.renderArray
                |> Deviations.render
        |> Page.render model dispatch this.NavManager
