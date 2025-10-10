namespace FunSharp.Blazor.Components

open System
open Bolero.Html
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module CheckBox =
    
    let render (update: bool -> unit) label isReadOnly currentValue =
        
        let update (newValue: bool) =
            if currentValue <> newValue then
                update newValue
                
        let guid = Guid.NewGuid()
                
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center
            
            comp<RadzenCheckBox<bool>> {
                attr.callback "Change" update
                
                "Name" => guid.ToString()
                "Value" => currentValue
                "ReadOnly" => isReadOnly
            }
            
            comp<RadzenLabel> {
                "Text" => label
                "Component" => guid.ToString()
            }
        }
