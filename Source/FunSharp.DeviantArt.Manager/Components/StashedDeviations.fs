namespace FunSharp.DeviantArt.Manager.Components

open Bolero.Html
open FunSharp.DeviantArt.Manager
open FunSharp.DeviantArt.Manager.Model
open Microsoft.JSInterop
open Radzen
open Radzen.Blazor
open FunSharp.DeviantArt.Api.Model

[<RequireQualifiedAccess>]
module StashedDeviations =
    
    let render parent (jsRuntime: IJSRuntime) (publish: StashedDeviation -> unit) (deviations: Loadable<StashedDeviation array>) =
        
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
                        "Image" => Some deviation.ImageUrl
                    }
                    
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Vertical
                        
                        div { text $"{deviation.ImageUrl.ToString()}" }
                        
                        comp<RadzenStack> {
                            "Orientation" => Orientation.Horizontal
                        
                            a {
                                attr.href (Helpers.stashUrl deviation.StashId)
                                attr.target "_blank"
                                "Open in Sta.sh"
                            }
                            
                            match deviation.Origin with
                            | DeviationOrigin.None -> ()
                            | DeviationOrigin.Prompt _ -> failwith "todo"
                            | DeviationOrigin.Inspiration inspiration ->
                                Helpers.copyToClipboard jsRuntime $"Inspired by {inspiration.Url}"
                                |> IconButton.render "Copy inspiration to clipboard"
                        }
            
                        Button.render parent (fun () -> publish deviation) "Publish"
                    }
                }
            )
            |> Helpers.renderArray
            
        | _ ->
            LoadingWidget.render ()
