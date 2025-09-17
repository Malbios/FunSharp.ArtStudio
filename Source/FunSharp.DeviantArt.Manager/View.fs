namespace FunSharp.DeviantArt.Manager

open Bolero.Html
open FunSharp.DeviantArt.Manager.Model
open Radzen.Blazor

module View =
    
    let view (model: Model.State) dispatch =
        
        concat {
            comp<RadzenComponents>
    
            div {
                attr.style "margin: 1rem;"
                
                cond model.Page
                <| function
                    | Page.Home
                    | Page.Inspirations -> ecomp<Pages.Inspirations,_,_> model dispatch { attr.empty() }
                    | Page.Prompts -> ecomp<Pages.Prompts,_,_> model dispatch { attr.empty() }
                    | Page.LocalDeviations -> ecomp<Pages.LocalDeviations,_,_> model dispatch { attr.empty() }
                    | Page.StashedDeviations -> ecomp<Pages.StashedDeviations,_,_> model dispatch { attr.empty() }
                    | Page.PublishedDeviations -> ecomp<Pages.PublishedDeviations,_,_> model dispatch { attr.empty() }
                    | Page.NotFound -> comp<Pages.NotFound> { attr.empty() }
            }
        }
