namespace FunSharp.Blazor.Components

open System
open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components

type ImagePreview() =
    inherit Component()
    
    [<Parameter>]
    member val Image : Uri option = None with get, set
    
    [<Parameter>]
    member val Clickable = false with get, set
    
    override this.Render() =
        
        let image url =
            img {
                attr.style "max-width: 200px; max-height: 200px;"
                attr.src url
            }
        
        div {
            match this.Image with
            | None -> LoadingWidget.render ()
            | Some url ->
                if this.Clickable then
                    a {
                        attr.href (url.ToString())
                        attr.target "_blank"
                        
                        url.ToString() |> image
                    }
                else
                    url.ToString() |> image
        }
