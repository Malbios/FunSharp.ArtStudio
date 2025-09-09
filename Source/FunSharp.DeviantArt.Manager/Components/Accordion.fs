namespace FunSharp.DeviantArt.Manager.Components

open Bolero
open Bolero.Html
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Accordion =
    
    type Item = {
        Text: string
        Render: unit -> Node
    }
    
    let render (items: Item array) =
        
        let accordionItems =
            [|
                for item in items do
                    yield comp<RadzenAccordionItem> {
                        "Text" => item.Text
                        
                        item.Render ()
                    }
            |]
        
        comp<RadzenAccordion> {
            "Items" => accordionItems
        }
