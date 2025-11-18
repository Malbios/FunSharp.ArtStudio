namespace FunSharp.ArtStudio.Client.Components

open Bolero
open Bolero.Html
open FunSharp.Blazor.Components
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Deviations =
    
    let render (children: Node array) =
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center
            "Wrap" => FlexWrap.Wrap
            "Gap" => "1rem"
            
            children |> Helpers.renderArray
        }
