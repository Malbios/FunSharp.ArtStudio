namespace FunSharp.Blazor.Components

open Bolero.Html
open Microsoft.AspNetCore.Components
open Radzen.Blazor

[<RequireQualifiedAccess>]
module TextInput =
    
    let render (onChange: string -> unit) (onInput: string -> unit) isReadOnly placeholder currentValue =
        
        let mutable text = currentValue
        
        comp<RadzenTextBox> {
            attr.callback "Change" onChange
            attr.callback "oninput" (fun (args: ChangeEventArgs) -> args.Value.ToString() |> onInput)
            
            "Value" => text
            "Placeholder" => placeholder
            "ReadOnly" => isReadOnly
        }
