namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Bolero.Html.attr
open FunSharp.DeviantArt.Manager
open Microsoft.AspNetCore.Components.Forms
open Radzen
open Radzen.Blazor
open FunSharp.DeviantArt.Manager.Components
open FunSharp.DeviantArt.Manager.Model

type Home() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.``FunSharp.DeviantArt.Manager``
    
    override this.ShouldRender(oldModel, newModel) =
        
        oldModel.IsBusy <> newModel.IsBusy ||
        oldModel.UploadedFiles <> newModel.UploadedFiles
        
    override this.View model dispatch =
        
        let uploadFiles (args: InputFileChangeEventArgs) =
            args.GetMultipleFiles(args.FileCount)
            |> Array.ofSeq
            |> Message.UploadFiles
            |> dispatch
        
        div {
            attr.``class`` "center-wrapper"
            
            comp<RadzenStack> {
                style "height: 100%"

                "JustifyContent" => JustifyContent.Center
                "AlignItems" => AlignItems.Center
                
                if model.IsBusy then
                    LoadingWidget.render ()
                else
                    FileInput.render true uploadFiles
                
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Horizontal
                        "Wrap" => FlexWrap.Wrap
                        "JustifyContent" => JustifyContent.Center
                        "AlignItems" => AlignItems.Center
                        
                        for file in model.UploadedFiles do
                            UploadedFile.render this dispatch file
                    }
                    
                    for file in model.StashedDeviations do
                        
                        let inspiration = file.Metadata.Inspiration |> Option.map _.ToString() |> Option.defaultValue ""
                        
                        div {
                            style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
                            
                            a { href $"{Helpers.stashUrl file.StashId}" }
                            text $"Inspired by {inspiration}"
                        }
            }
        }
