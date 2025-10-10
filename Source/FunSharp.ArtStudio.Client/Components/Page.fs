namespace FunSharp.ArtStudio.Client.Components

open Bolero
open Bolero.Html
open FunSharp.ArtStudio.Client.Model
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Page =
    
    let render (model: State) dispatch (content: Node) =
        
        div {
            attr.``class`` "center-wrapper"
            
            div {
                attr.style "height: 100%"
                
                comp<RadzenStack> {
                    "Orientation" => Orientation.Vertical
                    
                    Navigation.render model dispatch
                
                    content
                }
            }
        }
