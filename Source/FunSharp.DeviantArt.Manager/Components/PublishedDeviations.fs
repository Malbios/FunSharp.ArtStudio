namespace FunSharp.DeviantArt.Manager.Components

open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Api.Model

[<RequireQualifiedAccess>]
module PublishedDeviations =
    
    let render (deviations: Loadable<PublishedDeviation array>) =
        
        Loadable.render deviations
        <| fun deviations ->
            deviations
            |> Array.sortByDescending _.ImageUrl.ToString()
            |> Array.map (fun deviation -> Some deviation.ImageUrl |> Deviation.renderWithoutContent)
            |> Helpers.renderArray
            |> Deviations.render
