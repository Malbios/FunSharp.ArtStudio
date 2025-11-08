namespace FunSharp.ArtStudio.Client.Pages

open Bolero
open Bolero.Html
open FunSharp.ArtStudio.Model
open Microsoft.AspNetCore.Components
open Radzen
open FunSharp.Blazor.Components
open FunSharp.ArtStudio.Client.Model
open FunSharp.ArtStudio.Client.Components
open Radzen.Blazor

[<RequireQualifiedAccess>]
module PublishedDeviations =
    
    let deviationWidget (deviation: StatefulItem<PublishedDeviation>) =
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "AlignItems" => AlignItems.Center
            "Gap" => "0.2rem"
            
            let deviation = StatefulItem.valueOf deviation
            
            deviation
            |> _.ImageUrl
            |> Some
            |> Deviation.renderWithoutContent
            
            deviation
            |> _.Origin
            |> DeviationOrigin.inspiration
            |> function
                | None -> Node.Empty()
                | Some inspiration ->
                    Link.renderSimple (Some "Inspiration") inspiration.Url
                    
            CopyPrompt.render deviation.Origin
        }

type PublishedDeviations() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.PublishedDeviations
    
    override this.View model dispatch =
        
        let pageSize = FunSharp.ArtStudio.Client.Helpers.publishedDeviationsPageSize
        
        let changePage newPage =
            Message.LoadPublishedDeviationsPage (newPage * pageSize, pageSize) |> dispatch
            
        let publishedDeviationsCount =
            match model.PublishedDeviations with
            | Loadable.Loaded page -> page.total
            | _ -> -1
            
        match publishedDeviationsCount with
        | 0 -> text "No items."
        | _ ->
            Loadable.render model.PublishedDeviations
            <| fun page ->
                comp<RadzenStack> {
                    "Orientation" => Orientation.Vertical
                    "AlignItems" => AlignItems.Center
                    "Gap" => "3rem"
                    
                    div {
                        attr.style "margin-top: 2rem;"
                        Pager.render page.total pageSize page.offset changePage
                    }
                    
                    page.items
                    |> Array.map PublishedDeviations.deviationWidget
                    |> Helpers.renderArray
                    |> Deviations.render
                    
                    Pager.render page.total pageSize page.offset changePage
                }
        |> Page.render model dispatch
