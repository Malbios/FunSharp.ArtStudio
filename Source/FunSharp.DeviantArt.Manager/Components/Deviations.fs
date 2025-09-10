namespace FunSharp.DeviantArt.Manager.Components

open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Deviations =
    
    let render (content: Node) =
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center
            "Wrap" => FlexWrap.Wrap
            "Gap" => "1rem"
            
            content
        }
