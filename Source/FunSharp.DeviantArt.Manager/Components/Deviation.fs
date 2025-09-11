namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components

[<RequireQualifiedAccess>]
module Deviation =
    
    let private render (imageUrl: Uri option) (content: Node option) =
        
        comp<RadzenStack> {
            attr.style "margin: 0.25rem; padding: 0.5rem; border: 2px solid gray; border-radius: 8px; max-width: 700px;"

            "Orientation" => Orientation.Horizontal
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center

            comp<ImagePreview> {
                "Image" => imageUrl
            }

            match content with
            | None -> ()
            | Some content ->
                comp<RadzenStack> {
                    attr.style "margin: 0.5rem 0.5rem 0.5rem 0;"
                    
                    "Orientation" => Orientation.Vertical
                    
                    content
                }
        }
    
    let renderWithContent (imageUrl: Uri option) (content: Node) =
        render imageUrl (Some content)
    
    let renderWithoutContent (imageUrl: Uri option) =
        render imageUrl None
