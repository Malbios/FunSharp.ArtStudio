namespace FunSharp.DeviantArt.Manager.Components

open Bolero.Html
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components

[<RequireQualifiedAccess>]
module ClipboardSnippets =
    
    let render parent jsRuntime =
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            
            [
                ("really exaggerated",
                    " She is slim and very exceptionally stacked (really exaggerated).")
                ("low-poly",
                    "Stylized low-poly 3D aesthetic with simplified, angular geometry and minimal mesh detail, where forms are built from broad, flat-shaded polygons without normal maps or surface texturing. Colors are bright and saturated or purposefully pastel, with materials rendered in solid tones or subtle gradients, avoiding realism. Lighting is often ambient or softly directional, emphasizing readability over accuracy, and shadowing is either absent or flat. Linework is not used; instead, edges are defined by polygon contours and color separation. Characters and environments exhibit exaggerated proportions and cartoon-like minimalism, evoking a playful, game-ready visual language influenced by early 3D graphics and modern mobile or indie game design.")
            ]
            |> List.map (fun (label, text) -> Button.render parent (Helpers.copyToClipboard jsRuntime text) label)
            |> Helpers.renderList
        }
