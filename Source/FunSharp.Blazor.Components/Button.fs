namespace FunSharp.Blazor.Components

open System.Threading.Tasks
open Bolero.Html
open Microsoft.AspNetCore.Components.Web
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Button =
    
    let render (label: string) onClick (disabled: bool) =
        
        comp<RadzenButton> {
            "Text" => label
            "Disabled" => disabled
            
            attr.callback "Click" (fun (_: MouseEventArgs) -> onClick())
        }
        
    let renderAsync (label: string) (onClick: unit -> Task) (disabled: bool) =
        
        comp<RadzenButton> {
            "Text" => label
            "Disabled" => disabled
            
            attr.task.callback "Click" (fun (_: MouseEventArgs) -> onClick())
        }
