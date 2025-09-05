namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero.Html
open Radzen
open Radzen.Blazor
open FunSharp.DeviantArt.Api.Model

[<RequireQualifiedAccess>]
module UploadedFile =
            
    let private updateInspiration dispatch file (newValue: string) =
        let newValue =
            match newValue with
            | v when v.Trim() = "" -> None
            | v -> Uri v |> Some
            
        ()
        // Message.UpdateUploadedFile { file with Metadata.Inspiration = newValue } |> dispatch
        
    let private updateTitle dispatch file (newValue: string) =
        ()
        // Message.UpdateUploadedFile { file with Metadata.Title = newValue } |> dispatch
        
    let private updateGallery dispatch file (newValue: string) =
        ()
        // Message.UpdateUploadedFile { file with Metadata.Gallery = newValue } |> dispatch
    
    let render parent dispatch (galleries: Gallery[]) (deviation: LocalDeviation) =

        let galleries = galleries |> Array.map _.name        
        
        comp<RadzenStack> {
            attr.style "margin: 0.25rem; padding: 0.5rem; border: 2px solid gray; border-radius: 8px; max-width: 700px;"
            
            "Orientation" => Orientation.Horizontal
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center
            
            comp<ImagePreview> {
                "File" => deviation
            }
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Vertical
                
                div { text $"{deviation.Image.Name}" }
                
                deviation.Inspiration
                |> Option.map _.Url.ToString()
                |> Option.defaultValue ""
                |> TextInput.render (updateInspiration dispatch deviation) "Enter inspiration URL..."
                
                deviation.Title
                |> TextInput.render (updateTitle dispatch deviation) "Enter title..."
                
                deviation.Gallery
                |> DropDown.render (updateGallery dispatch deviation) "Gallery" "Select gallery..." galleries
                
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    "JustifyContent" => JustifyContent.Center
                    "AlignItems" => AlignItems.Center
                    
                    // Button.render parent (fun () -> dispatch (Message.SaveUploadedFile file)) "Save"
                    // Button.render parent (fun () -> dispatch (Message.Stash file)) "Stash"
                }
            }
        }
