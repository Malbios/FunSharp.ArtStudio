namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.Components

type PublishedDeviations() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.PublishedDeviations
    
    [<Inject>]
    member val NavManager: NavigationManager = Unchecked.defaultof<_> with get, set
    
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
                    deviation
                    |> StatefulItem.valueOf
                    |> _.ImageUrl
                    |> Some
                    |> Deviation.renderWithoutContent
                )
                |> Helpers.renderArray
                |> Deviations.render
        |> Page.render model dispatch this.NavManager
