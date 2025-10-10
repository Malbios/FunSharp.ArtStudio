namespace FunSharp.Blazor.Components

open Bolero.Html
open Radzen.Blazor

[<RequireQualifiedAccess>]
module TextAreaInput =
    
    let render (rows: int) (columns: int) (update: string -> unit) placeholder currentValue =
        
        let update (newValue: string) =
            if currentValue <> newValue then
                update newValue
        
        comp<RadzenTextArea> {
            attr.callback "Change" update
            
            // TODO: maybe needs this, maybe not
            // attr.callback "oninput" (fun (args: ChangeEventArgs) -> args.Value.ToString() |> update)
            
            "Value" => currentValue
            "Placeholder" => placeholder
            "Rows" => rows
            "Cols" => columns
        }
