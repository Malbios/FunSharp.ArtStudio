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
    
    override this.View model _ =
            
        let publishedDeviationsCount =
            match model.PublishedDeviations with
            | Loaded deviations -> deviations.Length
            | _ -> -1
            
        match publishedDeviationsCount with
        | 0 -> text "No items."
        | _ ->
            Loadable.render model.PublishedDeviations
            <| fun deviations ->
                deviations
                |> Array.sortByDescending _.ImageUrl.ToString()
                |> Array.map (fun deviation -> Some deviation.ImageUrl |> Deviation.renderWithoutContent)
                |> Helpers.renderArray
                |> Deviations.render
        |> Page.render this this.NavManager model
