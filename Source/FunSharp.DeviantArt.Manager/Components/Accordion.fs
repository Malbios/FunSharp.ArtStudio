namespace FunSharp.DeviantArt.Manager.Components

open Bolero
open Bolero.Html
open FunSharp.DeviantArt.Manager
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Accordion =
    
    type Item = {
        Label: string
        RenderAction: unit -> Node
    }
    
    let render (items: Item array) =
        
        let accordionItems =
            [|
                for item in items do
                    yield comp<RadzenAccordionItem> {
                        "Text" => item.Label
                        attr.fragment "ChildContent" (item.RenderAction ())
                    }
            |]
            |> Helpers.renderArray
        
        comp<RadzenAccordion> {
            attr.fragment "Items" accordionItems
        }
