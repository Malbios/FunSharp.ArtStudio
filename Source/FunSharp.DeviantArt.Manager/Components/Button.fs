namespace FunSharp.DeviantArt.Manager.Components

open Bolero.Html
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Forms
open Microsoft.AspNetCore.Components.Web
open Radzen.Blazor
open Toolbelt.Blazor.FileDropZone

[<RequireQualifiedAccess>]
module Button =
    
    let render parent (onClick: unit -> unit) label =
        
        let onClick = fun (_: MouseEventArgs) -> onClick ()
        
        comp<RadzenButton> {
            "Text" => label
            "Click" => EventCallback.Factory.Create<MouseEventArgs>(parent, onClick)
        }
