namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open FunSharp.DeviantArt.Model
open Microsoft.AspNetCore.Components
open Radzen
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.Components
open Radzen.Blazor

type PublishedDeviations() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.PublishedDeviations
    
    [<Inject>]
    member val NavManager: NavigationManager = Unchecked.defaultof<_> with get, set
    
    [<Inject>]
    member val DialogService = Unchecked.defaultof<DialogService> with get, set
    
    override this.View model dispatch =
            
        let publishedDeviationsCount =
            match model.PublishedDeviations with
            | Loadable.Loaded deviations -> deviations.Length
            | _ -> -1
            
        match publishedDeviationsCount with
        | 0 -> text "No items."
        | _ ->
            Loadable.render model.PublishedDeviations
            <| fun deviations ->
                deviations
                |> StatefulItems.sortByDescending _.ImageUrl.ToString()
                |> Array.map (fun deviation ->
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Vertical
                        "AlignItems" => AlignItems.Center
                        "Gap" => "0.2rem"
                        
                        deviation
                        |> StatefulItem.valueOf
                        |> _.ImageUrl
                        |> Some
                        |> Deviation.renderWithoutContent
                        
                        let inspirationUrl =
                            match (StatefulItem.valueOf deviation).Origin with
                            | DeviationOrigin.Inspiration inspiration -> Some inspiration.Url
                            | DeviationOrigin.Prompt prompt ->
                                match prompt.Inspiration with
                                | Some inspiration -> Some inspiration.Url
                                | None -> None
                            | _ -> None
                        
                        match inspirationUrl with
                        | Some url -> Link.renderSimple (Some "Inspiration") url
                        | None -> ()
                    }
                )
                |> Helpers.renderArray
                |> Deviations.render
        |> Page.render model dispatch this.NavManager this.DialogService
