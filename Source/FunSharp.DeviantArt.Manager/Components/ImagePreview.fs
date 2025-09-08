namespace FunSharp.DeviantArt.Manager.Components

open Bolero
open Bolero.Html
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager.Model
open Microsoft.AspNetCore.Components

type ImagePreview() =
    inherit Component()

    [<Parameter>]
    member val Image : Loadable<Image> option = None with get, set
    
    member this.TriggerReRender() =
        
        this.StateHasChanged()

    override this.Render() =
        div {
            match this.Image with
            | None -> LoadingWidget.render ()
            | Some x ->
                match x with
                | Loaded image ->
                    img {
                        attr.style "max-width: 200px; max-height: 200px;"
                        attr.src (image.AsUrl())
                    }
                | _ -> LoadingWidget.render ()
        }
