namespace FunSharp.DeviantArt.Manager.Components

open Bolero.Html
open Radzen.Blazor

[<RequireQualifiedAccess>]
module TextInput =
    
    let render (update: string -> unit) placeholder currentValue =
        
        let update (newValue: string) =
            if currentValue <> newValue then
                update newValue
        
        comp<RadzenTextBox> {
            attr.callback "Change" update
            // attr.callback "oninput" (fun (args: ChangeEventArgs) -> args.Value.ToString() |> update)
            
            "Value" => currentValue
            "Placeholder" => placeholder
        }
