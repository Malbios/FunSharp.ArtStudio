namespace FunSharp.DeviantArt.Manager.Components

open Bolero.Html
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Model

[<RequireQualifiedAccess>]
module ClipboardSnippets =
    
    let render parent jsRuntime (snippets: ClipboardSnippet array option) =
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            
            match snippets with
            | None -> ()
            | Some snippets ->
                snippets
                |> Array.map (fun snippet ->
                    Button.render parent (Helpers.copyToClipboard jsRuntime snippet.value) false snippet.label
                )
                |> Helpers.renderArray
        }
