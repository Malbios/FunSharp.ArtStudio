namespace FunSharp.ArtStudio.Client.Components

open Bolero.Html
open FunSharp.ArtStudio.Model
open FunSharp.Blazor.Components
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Inspiration =
    
    let render (inspiration: Inspiration option) =
        
        match inspiration with
        | None -> div { attr.style "width: 50px; height: 50px; background-color: grey;" }
            
        | Some inspiration ->
            comp<RadzenStack> {
                "Orientation" => Orientation.Vertical
                "JustifyContent" => JustifyContent.Center
                "AlignItems" => AlignItems.Center
                "Gap" => "0.2rem"
                
                comp<Image> {
                    "ImageUrl" => inspiration.ImageUrl
                    "ClickUrl" => Some inspiration.Url
                }
                
                Link.renderSimple (Some "DA Link") inspiration.Url
            }
