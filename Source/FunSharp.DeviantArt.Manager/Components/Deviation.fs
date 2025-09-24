namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components

[<RequireQualifiedAccess>]
module Deviation =
    
    let private render (inspirationImageUrl: Uri option) (imageUrl: Uri option) (content: Node option) =
        
        comp<RadzenStack> {
            attr.style "margin: 0.25rem; padding: 0.5rem; max-width: 700px;"

            "Orientation" => Orientation.Horizontal
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Vertical
                
                match inspirationImageUrl with
                | None -> ()
                | Some v ->
                    div {
                        attr.style "margin: 0.5rem;"
                        
                        comp<ImagePreview> {
                            "Image" => Some v
                            "Clickable" => true
                        }
                    }
                
                match imageUrl with
                | None -> ()
                | Some v ->
                    div {
                        attr.style "margin: 0.5rem;"
                        
                        comp<ImagePreview> {
                            "Image" => Some v
                            "Clickable" => true
                        }
                    }
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
    
    let renderWithContent (inspirationImageUrl: Uri option) (imageUrl: Uri option) (content: Node) =
        render inspirationImageUrl imageUrl (Some content)
    
    let renderWithoutContent (imageUrl: Uri option) =
        render None imageUrl None
