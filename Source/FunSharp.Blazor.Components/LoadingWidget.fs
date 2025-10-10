namespace FunSharp.Blazor.Components

open Bolero.Html
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module LoadingWidget =
    
    let render () =
        
        comp<RadzenProgressBarCircular> {
            attr.style "width: 100px;"
            
            "ShowValue" => false
            "Mode" => ProgressBarMode.Indeterminate
        }
