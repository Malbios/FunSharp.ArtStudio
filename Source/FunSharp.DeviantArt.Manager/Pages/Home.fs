namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Bolero.Html.attr
open Microsoft.AspNetCore.Components.Forms
open Radzen
open Radzen.Blazor
open FunSharp.DeviantArt.Manager.Components
open FunSharp.DeviantArt.Manager.Model

type Home() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.``FunSharp.DeviantArt.Manager``
    
    // override this.ShouldRender(oldModel, newModel) =
    //     
    //     oldModel.UploadedFiles <> newModel.UploadedFiles
        
    override this.View model dispatch =
        
        let uploadFiles (args: InputFileChangeEventArgs) =
            args.GetMultipleFiles(args.FileCount)
            |> Array.ofSeq
            |> Message.UploadLocalDeviations
            |> dispatch
        
        div {
            attr.``class`` "center-wrapper"
            
            comp<RadzenStack> {
                style "height: 100%"

                "JustifyContent" => JustifyContent.Center
                "AlignItems" => AlignItems.Center
                
                FileInput.render true uploadFiles
                
                match model.LocalDeviations with
                | Loaded deviations ->
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Horizontal
                        "Wrap" => FlexWrap.Wrap
                        "JustifyContent" => JustifyContent.Center
                        "AlignItems" => AlignItems.Center
                        
                        for deviation in deviations do
                            UploadedFile.render this dispatch deviation
                    }
                    
                | NotLoaded
                | Loading ->
                    LoadingWidget.render ()
                
                | _ -> ()
                
                // for file in model.StashedDeviations do
                //     
                //     let inspiration = file.Metadata.Inspiration |> Option.map _.ToString() |> Option.defaultValue ""
                //     
                //     div {
                //         style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
                //         
                //         a { href $"{Helpers.stashUrl file.StashId}" }
                //         text $"Inspired by {inspiration}"
                //     }
            }
        }
