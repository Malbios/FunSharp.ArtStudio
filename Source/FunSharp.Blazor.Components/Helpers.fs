namespace FunSharp.Blazor.Components

open Bolero
open Bolero.Html
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
        
    let copyToClipboard (js: IJSRuntime) (text: string) =
        
        js.InvokeVoidAsync("navigator.clipboard.writeText", [| box text |]) |> ignore
        
    let readClipboard (js: IJSRuntime) =
        
        js.InvokeAsync<string>("navigator.clipboard.readText").AsTask() |> Async.AwaitTask
        
    let openInNewTab (js: IJSRuntime) (url: string) =
        
        js.InvokeVoidAsync($"window.open", [| box url; box "_blank" |]) |> ignore
