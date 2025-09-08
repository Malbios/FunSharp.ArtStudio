namespace FunSharp.DeviantArt.Manager.Components

open Bolero.Html
open FunSharp.DeviantArt.Manager
open FunSharp.DeviantArt.Manager.Model
open Radzen
open Radzen.Blazor
open FunSharp.DeviantArt.Api.Model

[<RequireQualifiedAccess>]
module PublishedDeviations =
    
    let render (images: Map<string, Loadable<Image>>) (deviations: Loadable<PublishedDeviation array>) =
        
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
                        "Image" => (images |> Map.tryFind deviation.Metadata.Id)
                    }
            
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Vertical
            
                        div { text $"{deviation.Metadata.Id}" }
            
                        div { text $"{deviation.Metadata.Title}" }
                        
                        a {
                            attr.href deviation.Url
                            attr.target "_blank"
                            
                            $"{deviation.Url}"
                        }
                    }
                }
            )
            |> Helpers.renderArray
            
        | _ ->
            LoadingWidget.render ()
