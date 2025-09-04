namespace FunSharp.DeviantArt.Manager.Pages

open Microsoft.AspNetCore.Components.Forms
open System
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
open FunSharp.Common
open FunSharp.DeviantArt.Manager.Common
open FunSharp.DeviantArt.Manager.Components
open FunSharp.DeviantArt.Manager.Model

type Home() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.``FunSharp.DeviantArt.Manager``
    
    override this.ShouldRender(oldModel, newModel) =
        
        oldModel.IsBusy <> newModel.IsBusy ||
        oldModel.UploadedFiles <> newModel.UploadedFiles
        
    override this.View model dispatch =
            
        let updateInspiration file (newValue: string) =
            let newValue =
                match newValue with
                | v when v.Trim() = "" -> None
                | v -> Uri v |> Some
            
            Message.UpdateUploadedFile { file with Metadata.Inspiration = newValue } |> dispatch
            
        let updateTitle file (newValue: string) =
            Message.UpdateUploadedFile { file with Metadata.Title = newValue } |> dispatch
            
        let updateGallery file (newValue: string) =
            Message.UpdateUploadedFile { file with Metadata.Gallery = newValue } |> dispatch
        
        let uploadFiles (args: InputFileChangeEventArgs) =
            args.GetMultipleFiles(args.FileCount)
            |> Array.ofSeq
            |> Message.UploadFiles
            |> dispatch
        
        div {
            attr.``class`` "center-wrapper"
            
            comp<RadzenStack> {
                attr.style "height: 100%"

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
                            comp<RadzenStack> {
                                attr.style "margin: 0.25rem; padding: 0.5rem; border: 2px solid gray; border-radius: 8px; max-width: 700px;"
                                
                                "Orientation" => Orientation.Horizontal
                                "JustifyContent" => JustifyContent.Center
                                "AlignItems" => AlignItems.Center
                                
                                comp<ImagePreview> {
                                    "File" => file
                                }
                                
                                comp<RadzenStack> {
                                    "Orientation" => Orientation.Vertical
                                    
                                    div { text $"{file.FileName}" }
                                    
                                    file.Metadata.Inspiration
                                    |> Option.map _.ToString()
                                    |> Option.defaultValue ""
                                    |> TextInput.render (updateInspiration file) "Enter inspiration URL..."
                                    
                                    file.Metadata.Title
                                    |> TextInput.render (updateTitle file) "Enter title..."
                                    
                                    file.Metadata.Gallery
                                    |> DropDown.render (updateGallery file) "Gallery" "Select gallery..." (Union.asStrings<ImageType>())
                                    
                                    comp<RadzenStack> {
                                        "Orientation" => Orientation.Horizontal
                                        "JustifyContent" => JustifyContent.Center
                                        "AlignItems" => AlignItems.Center
                                        
                                        Button.render this (fun () -> dispatch (Message.SaveUploadedFile file)) "Save"
                                        Button.render this (fun () -> dispatch (Message.Stash file)) "Stash"
                                    }
                                }
                            }
                    }
            }
        }
