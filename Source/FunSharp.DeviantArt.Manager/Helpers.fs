namespace FunSharp.DeviantArt.Manager

open Bolero
open Bolero.Html
open FunSharp.Common
open Microsoft.AspNetCore.Components.Web
open Microsoft.JSInterop

[<RequireQualifiedAccess>]
module Helpers =
    
    let renderArray (nodes: Node array) =
        concat {
            for node in nodes do node
        }
    
    let renderList (nodes: Node list) =
        concat {
            for node in nodes do node
        }
    
    let stashUrl itemId =
        
        $"https://sta.sh/0{Base36.encode itemId}"
        
    let copyToClipboard (jsRuntime: IJSRuntime) (text: string) : unit -> unit =
        fun () -> jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", [| box text |]) |> ignore
