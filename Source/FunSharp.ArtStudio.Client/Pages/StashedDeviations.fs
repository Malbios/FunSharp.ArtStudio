namespace FunSharp.ArtStudio.Client.Pages

open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Api.Model.Helpers
open FunSharp.ArtStudio.Model
open FunSharp.ArtStudio.Client.Model
open FunSharp.ArtStudio.Client.Components

type StashedDeviations() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.StashedDeviations
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<_> with get, set
    
    override this.View model dispatch =
        
        let publish deviation =
            
            dispatch <| Message.PublishStashed deviation
            
        let forget deviation =
            
            dispatch <| Message.ForgetStashedDeviation deviation
        
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
                        let inspiration = DeviationOrigin.inspiration deviation.Origin
                        
                        let copyInspirationToClipboard =
                            match inspiration with
                            | None -> None
                            | Some inspiration ->
                                fun () ->
                                    Helpers.copyToClipboard this.JSRuntime $"Inspired by {inspiration.Url}"
                                |> Some
                        
                        let inspirationUrl =
                            match inspiration with
                            | None -> None
                            | Some inspiration -> inspiration.ImageUrl
                            
                        Deviation.renderWithContent inspirationUrl (Some deviation.ImageUrl)
                        <| comp<RadzenStack> {
                            "Orientation" => Orientation.Vertical
                            
                            deviation.Timestamp.ToString() |> text
                            
                            Link.render copyInspirationToClipboard (Some "Open in Sta.sh")
                                $"{stashEditUrl deviation.StashId}"
                            
                            comp<RadzenStack> {
                                "Orientation" => Orientation.Horizontal
                                "JustifyContent" => JustifyContent.SpaceBetween
                                
                                Button.renderSimple "Publish" <| fun () -> publish deviation
                                
                                CopyPrompt.render deviation.Origin
                                
                                Button.renderSimple "Forget" <| fun () -> forget deviation
                            }
                        }
                )
                |> Deviations.render
        |> Page.render model dispatch
