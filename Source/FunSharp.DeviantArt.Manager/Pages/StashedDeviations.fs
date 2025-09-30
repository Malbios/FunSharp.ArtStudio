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
            | Loadable.Loaded deviations -> deviations.Length
            | _ -> -1

        match stashedDeviationsCount with
        | 0 -> text "No items."
        | _ ->
            Loadable.render model.StashedDeviations
            <| fun deviations ->
                deviations
                |> StatefulItems.sortBy _.Timestamp
                |> Array.map (fun deviation ->
                    match deviation with
                    | StatefulItem.IsBusy _ ->
                        LoadingWidget.render ()
                        
                    | StatefulItem.HasError (deviation, error) ->
                        concat {
                            text $"deviation: {StashedDeviation.keyOf deviation}"
                            text $"error: {error}"
                        }
                        
                    | StatefulItem.Default deviation ->
                        let copyInspirationToClipboard =
                            match deviation.Origin with
                            | DeviationOrigin.None -> None
                            | DeviationOrigin.Inspiration inspiration ->
                                Some <| fun () -> Helpers.copyToClipboard this.JSRuntime $"Inspired by {inspiration.Url}"
                            | DeviationOrigin.Prompt prompt ->
                                match prompt.Inspiration with
                                | None -> None
                                | Some inspiration ->
                                    Some <| fun () -> Helpers.copyToClipboard this.JSRuntime $"Inspired by {inspiration.Url}"
                        
                        let inspirationUrl =
                            match deviation.Origin with
                            | DeviationOrigin.None -> None
                            | DeviationOrigin.Prompt prompt -> prompt.Inspiration |> Option.bind _.ImageUrl
                            | DeviationOrigin.Inspiration inspiration -> inspiration.ImageUrl
                            
                        Deviation.renderWithContent inspirationUrl (Some deviation.ImageUrl)
                        <| comp<RadzenStack> {
                            "Orientation" => Orientation.Vertical
                            
                            deviation.Timestamp.ToString() |> text
                            
                            Link.render copyInspirationToClipboard (Some "Open in Sta.sh")
                                $"{Helpers.stashEditUrl deviation.StashId}"
                            
                            Button.render "Publish" (fun () -> publish deviation) false
                        }
                )
                |> Helpers.renderArray
                |> Deviations.render
        |> Page.render model dispatch this.NavManager
