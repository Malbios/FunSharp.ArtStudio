namespace FunSharp.Blazor.Components

open Bolero
open Bolero.Html
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Tabs =
    
    type Item = {
        Key: string
        Label: string
        RenderAction: unit -> Node
    }
    
    let render (items: Item array) =
        
        let tabs =
            [|
                for item in items do
                    comp<RadzenTabsItem> {
                        attr.key item.Key
                        "Text" => item.Label
                        attr.fragment "ChildContent" (item.RenderAction ())
                    }
            |]
            |> Helpers.renderArray
        
        comp<RadzenTabs> {
            attr.fragment "Tabs" tabs
        }
