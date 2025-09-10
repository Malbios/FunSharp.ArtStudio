namespace FunSharp.DeviantArt.Manager.Components

open Bolero.Html
open Microsoft.JSInterop
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager

[<RequireQualifiedAccess>]
module StashedDeviations =
    
    let render parent (jsRuntime: IJSRuntime) (publish: StashedDeviation -> unit) (deviations: Loadable<StashedDeviation array>) =
        
        Loadable.render deviations
        <| fun deviations ->
            deviations
            |> Array.map (fun deviation ->
                concat {
                    deviation.ImageUrl
                    |> Some
                    |> ImageUrl.render
                    
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Horizontal
                        
                        comp<RadzenLink> {
                            "Path" => $"{Helpers.stashUrl deviation.StashId}"
                            "Text" => "Open in Sta.sh"
                            "Target" => "_blank"
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
                |> Deviation.render (Some deviation.ImageUrl)
            )
            |> Helpers.renderArray
