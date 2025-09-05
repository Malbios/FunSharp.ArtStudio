namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor
open FunSharp.DeviantArt.Api.Model

type LocalDeviationEditor() =
    inherit Component()
    
    let mutable draft = LocalDeviation.empty
        
    let updateInspiration (url: string) =
        draft <- {
            draft with
                Inspiration = { Url = Uri url; Image = Image.empty } |> Some
        }
    
    let updateTitle (title: string) =
        draft <- {
            draft with
                Title = title
        }
    
    let updateGallery (gallery: string) =
        draft <- {
            draft with
                Gallery = gallery
        }
    
    [<Parameter>]
    member val Galleries = Array.empty<string> with get, set
    
    [<Parameter>]
    member val Item = draft with get, set

    [<Parameter>]
    member val OnSave : LocalDeviation -> unit = ignore with get, set

    [<Parameter>]
    member val OnStash : LocalDeviation -> unit = ignore with get, set
    
    override this.OnParametersSet() =
        
        draft <- this.Item
    
    override this.Render() =
        
        comp<RadzenStack> {
         attr.style "margin: 0.25rem; padding: 0.5rem; border: 2px solid gray; border-radius: 8px; max-width: 700px;"
         
         "Orientation" => Orientation.Horizontal
         "JustifyContent" => JustifyContent.Center
         "AlignItems" => AlignItems.Center
         
         comp<ImagePreview> {
             "Image" => draft.Image
         }
         
         comp<RadzenStack> {
             "Orientation" => Orientation.Vertical
             
             div { text $"{draft.Image.Name}" }
             
             draft.Inspiration
             |> Option.map _.Url.ToString()
             |> Option.defaultValue ""
             |> TextInput.render updateInspiration "Enter inspiration URL..."
             
             draft.Title
             |> TextInput.render updateTitle "Enter title..."
             
             draft.Gallery
             |> DropDown.render updateGallery "Gallery" "Select gallery..." this.Galleries
             
             comp<RadzenStack> {
                 "Orientation" => Orientation.Horizontal
                 "JustifyContent" => JustifyContent.Center
                 "AlignItems" => AlignItems.Center
                 
                 Button.render this (fun () -> this.OnSave draft) "Save"
                 Button.render this (fun () -> this.OnStash draft) "Stash"
             }
         }
     }
