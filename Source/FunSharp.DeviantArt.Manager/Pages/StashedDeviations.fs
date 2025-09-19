namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager
open Microsoft.AspNetCore.Components
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.Components
open Radzen
open Radzen.Blazor

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
                |> Array.sortBy _.Timestamp
                |> Array.map (fun deviation ->
                    let inspirationUrl =
                        match deviation.Origin with
                        | DeviationOrigin.None -> None
                        | DeviationOrigin.Prompt prompt -> prompt.Inspiration |> Option.bind _.ImageUrl
                        | DeviationOrigin.Inspiration inspiration -> inspiration.ImageUrl
                            
                    concat {
                        deviation.ImageUrl
                        |> Some
                        |> ImageUrl.render
                        
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
                        
                        Button.render this (fun () -> publish deviation) false "Publish"
                    }
                    |> Deviation.renderWithContent inspirationUrl (Some deviation.ImageUrl)
                )
                |> Helpers.renderArray
                |> Deviations.render
        |> Page.render this model dispatch this.NavManager
