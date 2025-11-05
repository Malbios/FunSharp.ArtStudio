namespace FunSharp.Blazor.Components

open System
open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components

type Image() =
    inherit Component()
    
    [<Parameter>]
    member val ImageUrl : Uri option = None with get, set
    
    [<Parameter>]
    member val ClickUrl : Uri option = None with get, set
    
    override this.Render() =
        
        let image url = img {
            attr.style "max-width: 200px; max-height: 200px;"
            attr.src url
        }
        
        match this.ImageUrl with
        | None -> LoadingWidget.render ()
        | Some imageUrl ->
            match this.ClickUrl with
            | None -> image imageUrl
            | Some clickUrl ->
                a {
                    attr.href clickUrl
                    attr.target "_blank"
                    
                    image imageUrl
                }
