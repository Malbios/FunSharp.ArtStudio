namespace FunSharp.DeviantArt.Manager.Components

open Bolero.Html
open FunSharp.DeviantArt.Manager
open FunSharp.DeviantArt.Manager.Model
open Radzen
open Radzen.Blazor
open FunSharp.DeviantArt.Api.Model

[<RequireQualifiedAccess>]
module StashedDeviations =
    
    let render parent (publish: StashedDeviation -> unit) (deviations: Loadable<StashedDeviation array>) =
        
        match deviations with
        | Loaded deviations ->
            deviations
            |> Array.map (fun deviation ->
                comp<RadzenStack> {
                    attr.style "margin: 0.25rem; padding: 0.5rem; border: 2px solid gray; border-radius: 8px; max-width: 700px;"
            
                    "Orientation" => Orientation.Horizontal
                    "JustifyContent" => JustifyContent.Center
                    "AlignItems" => AlignItems.Center
            
                    comp<ImagePreview> {
                        "Image" => deviation.Metadata.Image
                    }
            
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Vertical
            
                        div { text $"{deviation.Metadata.Image.Name}" }
                        
                        a {
                            attr.href (Helpers.stashUrl deviation.StashId)
                            "Open in Sta.sh"
                        }
            
                        Button.render parent (fun () -> publish deviation) "Publish"
                    }
                }
            )
            |> Helpers.renderArray
            
        | _ ->
            LoadingWidget.render ()
