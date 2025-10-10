namespace FunSharp.Blazor.Components

open Bolero
open Bolero.Html
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Accordion =
    
    type Item = {
        Label: string
        Expanded: bool
        RenderAction: unit -> Node
    }
    
    let render (allowMultipleOpen: bool) (items: Item array) =
        
        let accordionItems =
            [|
                for item in items do
                    yield comp<RadzenAccordionItem> {
                        "Text" => item.Label
                        "Selected" => item.Expanded
                        attr.fragment "ChildContent" (item.RenderAction ())
                    }
            |]
            |> Helpers.renderArray
        
        comp<RadzenAccordion> {
            "Multiple" => allowMultipleOpen
            attr.fragment "Items" accordionItems
        }
