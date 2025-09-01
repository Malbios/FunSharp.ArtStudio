namespace FunSharp.DeviantArt.Manager

open Bolero.Html
open FunSharp.DeviantArt.Manager.Model
open Radzen.Blazor

module View =
    
    let view (model: Application.State) _ =
    
        concat {
            comp<RadzenComponents>
    
            div {
                attr.style "margin: 1rem;"
                
                cond model.Page
                <| function
                    | Page.Home -> comp<Pages.Home> { attr.empty() }
                    | Page.NotFound -> comp<Pages.NotFound> { attr.empty() }
                    | Page.Test -> comp<Pages.Test> { attr.empty() }
            }
        }
