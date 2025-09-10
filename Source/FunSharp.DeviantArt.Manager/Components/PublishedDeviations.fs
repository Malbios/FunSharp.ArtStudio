namespace FunSharp.DeviantArt.Manager.Components

open Bolero.Html
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Api.Model

[<RequireQualifiedAccess>]
module PublishedDeviations =
    
    let render (deviations: Loadable<PublishedDeviation array>) =
        
        Loadable.render deviations
        <| fun deviations ->
            deviations
            |> Array.map (fun deviation ->
                concat {
                    deviation.ImageUrl
                    |> Some
                    |> ImageUrl.render
                    
                    comp<RadzenLink> {
                        "Path" => $"{deviation.Url}"
                        "Text" => $"{deviation.Metadata.Title}"
                        "Target" => "_blank"
                    }
                }
                |> Deviation.render (Some deviation.ImageUrl)
            )
            |> Helpers.renderArray
