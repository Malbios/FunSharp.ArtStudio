namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components

type ImagePreview() =
    inherit Component()
    
    [<Parameter>]
    member val Image : Uri option = None with get, set
    
    override this.Render() =
        div {
            match this.Image with
            | None -> LoadingWidget.render ()
            | Some url ->
                img {
                    attr.style "max-width: 200px; max-height: 200px;"
                    attr.src (url.ToString())
                }
        }
