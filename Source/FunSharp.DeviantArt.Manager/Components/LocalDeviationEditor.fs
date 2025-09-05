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
    
    [<Parameter>]
    member val Galleries = Array.empty<string> with get, set
    
    [<Parameter>]
    member val Deviation = LocalDeviation.empty with get, set

    [<Parameter>]
    member val OnSave : LocalDeviation -> unit = ignore with get, set

    [<Parameter>]
    member val OnStash : LocalDeviation -> unit = ignore with get, set
    
    override this.Render() =
    
        let withNewInspiration (newUrl: string) =
            this.Deviation <- { this.Deviation with Inspiration = Some { Url = Uri newUrl; Image = Image.empty } }
        
        let withNewTitle (newTitle: string) =
            this.Deviation <- { this.Deviation with Title = newTitle }
        
        let withNewGallery (newGallery: string) =
            this.Deviation <- { this.Deviation with Gallery = newGallery }
        
        comp<RadzenStack> {
            attr.style "margin: 0.25rem; padding: 0.5rem; border: 2px solid gray; border-radius: 8px; max-width: 700px;"

            "Orientation" => Orientation.Horizontal
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center

            comp<ImagePreview> {
                "Image" => this.Deviation.Image
            }

            comp<RadzenStack> {
                "Orientation" => Orientation.Vertical

                div { text $"{this.Deviation.Image.Name}" }

                this.Deviation.Inspiration
                |> Option.map _.Url.ToString()
                |> Option.defaultValue ""
                |> TextInput.render withNewInspiration "Enter inspiration URL..."

                this.Deviation.Title
                |> TextInput.render withNewTitle "Enter title..."

                this.Deviation.Gallery
                |> DropDown.render withNewGallery "Gallery" "Select gallery..." this.Galleries

                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    "JustifyContent" => JustifyContent.Center
                    "AlignItems" => AlignItems.Center

                    Button.render this (fun () -> this.OnSave this.Deviation) "Save"
                    Button.render this (fun () -> this.OnStash this.Deviation) "Stash"
                }
            }
        }
