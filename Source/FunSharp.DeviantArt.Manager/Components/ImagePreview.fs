namespace FunSharp.DeviantArt.Manager.Components

open Bolero
open Bolero.Html
open FunSharp.DeviantArt.Manager.Model
open Microsoft.AspNetCore.Components

type ImagePreview() =
    inherit Component()

    [<Parameter>]
    member val File: UploadedFile = UploadedFile.empty with get, set
    
    member this.TriggerReRender() =
        
        this.StateHasChanged()

    override this.Render() =
        div {
            match this.File.PreviewUrl with
            | url when url <> "" ->
                img {
                    attr.style "max-width: 200px; max-height: 200px;"
                    attr.src url
                }
                
            | _ ->
                LoadingWidget.render ()
        }
