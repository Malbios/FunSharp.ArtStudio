namespace FunSharp.DeviantArt.Manager.Components

open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Page =
    
    let render parent (nav: NavigationManager) (content: Node) =
        
        div {
            attr.``class`` "center-wrapper"
            
            div {
                attr.style "height: 100%"
                
                comp<RadzenStack> {
                    "Orientation" => Orientation.Vertical
                    
                    Navigation.render parent nav
                
                    content
                }
            }
        }
