namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components

[<RequireQualifiedAccess>]
module Deviation =
    
    let render (imageUrl: Uri option) (content: Node) =
        
        comp<RadzenStack> {
            attr.style "margin: 0.25rem; padding: 0.5rem; border: 2px solid gray; border-radius: 8px; max-width: 700px;"

            "Orientation" => Orientation.Horizontal
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center

            div {
                attr.style "margin-left: 0.5rem;"
                
                comp<ImagePreview> {
                    "Image" => imageUrl
                }
            }

            comp<RadzenStack> {
                attr.style "margin: 0.5rem 0.5rem 0.5rem 0;"
                
                "Orientation" => Orientation.Vertical
                
                content
            }
        }
