namespace FunSharp.DeviantArt.Manager.Pages

open System
open Bolero
open Bolero.Html
open FunSharp.DeviantArt.Manager.Components
open FunSharp.DeviantArt.Manager.Model.Application
open FunSharp.DeviantArt.Manager.Model.Common
open FunSharp.Common
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Forms
open Microsoft.AspNetCore.Components.Web
open Radzen
open Radzen.Blazor
open Toolbelt.Blazor.FileDropZone

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
            |> Message.UploadImages
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
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Horizontal
                        "JustifyContent" => JustifyContent.Center
                        "AlignItems" => AlignItems.Center
                        
                        comp<FileDropZone> {
                            attr.style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
                            
                            comp<InputFile> {
                              attr.multiple true
                              attr.callback "OnChange" uploadFiles
                            }
                        }
                        
                        comp<RadzenButton> {
                            let onClick (_: MouseEventArgs) = dispatch Message.LoadDeviations
                            
                            "Text" => "Load"
                            "Click" => EventCallback.Factory.Create<MouseEventArgs>(this, onClick)
                        }
                    }
                
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
                                    
                                    comp<RadzenButton> {
                                        let onClick (_: MouseEventArgs) = dispatch (Message.Stash file)
                                        
                                        "Text" => "Stash"
                                        "Click" => EventCallback.Factory.Create<MouseEventArgs>(this, onClick)
                                    }
                                }
                            }
                    }
            }
        }
