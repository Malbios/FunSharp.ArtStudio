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
                    | Page.Home -> ecomp<Pages.Home,_,_> model dispatch { attr.empty() }
                    | Page.NotFound -> comp<Pages.NotFound> { attr.empty() }
            }
        }
