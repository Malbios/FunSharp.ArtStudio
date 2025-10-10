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
    member val JSRuntime = Unchecked.defaultof<_> with get, set
    
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
                                
                        deviation
                        |> _.Origin
                        |> DeviationOrigin.prompt
                        |> function
                            | None -> Node.Empty()
                            | Some prompt ->
                                Button.render "Copy Prompt" (fun () -> Helpers.copyToClipboard this.JSRuntime prompt.Text) false
                    }
                )
                |> Helpers.renderArray
                |> Deviations.render
        |> Page.render model dispatch
