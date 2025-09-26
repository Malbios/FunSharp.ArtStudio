namespace FunSharp.DeviantArt.Manager.Components

open Bolero.Html
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Model

[<RequireQualifiedAccess>]
module ClipboardSnippets =
    
    let render jsRuntime (snippets: ClipboardSnippet array) =
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            
            snippets
            |> Array.map (fun snippet ->
                Button.render snippet.label (fun () -> Helpers.copyToClipboard jsRuntime snippet.value) false
            )
            |> Helpers.renderArray
        }
