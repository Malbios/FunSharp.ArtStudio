namespace FunSharp.Blazor.Components

open System
open Bolero.Html
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module DropDown =
    
    let render (update: string -> unit) label isReadOnly placeholder values currentValue =
        
        let update (newValue: string) =
            if currentValue <> newValue then
                update newValue
        
        let guid = Guid.NewGuid()
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            "AlignItems" => AlignItems.Center
            "JustifyContent" => JustifyContent.Center
            "Gap" => "0.5rem"
            
            comp<RadzenLabel> {
                "Text" => label
                "Component" => guid.ToString()
            }
            
            comp<RadzenDropDown<string>> {
                attr.callback "Change" (fun (value: obj) -> value.ToString() |> update)
                
                "TValue" => "string"
                "Name" => guid.ToString()
                "Placeholder" => placeholder
                "Data" => values
                "Value" => currentValue
                "ReadOnly" => isReadOnly
            }
        }
