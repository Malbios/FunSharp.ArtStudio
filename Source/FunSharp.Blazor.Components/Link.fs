namespace FunSharp.Blazor.Components

open System
open Bolero.Html
open Microsoft.AspNetCore.Components.Web
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Link =
    
    let private linkLabel text url =
        
        match text with
        | Some x -> x
        | None -> url.ToString()
        
    let render (action: (unit -> unit) option) (text: string option) (url: Uri) =
        
        let linkLabel = linkLabel text url
        
        comp<RadzenLink> {
            "Path" => $"{url}"
            "Text" => linkLabel
            "Target" => "_blank"
            
            match action with
            | Some action -> attr.callback "onclick" (fun (_:MouseEventArgs) -> action ())
            | None -> attr.empty ()
        }
    
    let renderSimple (text: string option) (url: Uri) =

        render None text url
