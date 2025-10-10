namespace FunSharp.Blazor.Components

open Bolero.Html
open Microsoft.AspNetCore.Components.Web

[<RequireQualifiedAccess>]
module IconButton =
    
    let render title (onClick: unit -> unit) =
        
        button {
            attr.``type`` "button"
            attr.title title
            
            on.click (fun (_:MouseEventArgs) -> onClick ())
            
            i { attr.``class`` "fas fa-copy" }
        }
