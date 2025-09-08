namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero
open Bolero.Html
open FunSharp.DeviantArt.Manager.Model
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
    member val Image : Loadable<Image> option = None with get, set

    [<Parameter>]
    member val OnSave : LocalDeviation -> unit = ignore with get, set

    [<Parameter>]
    member val OnStash : LocalDeviation -> unit = ignore with get, set
    
    override this.Render() =
    
        let withNewInspiration (newUrl: string) =
            this.Deviation <- { this.Deviation with Inspiration = Some { Id = Guid.NewGuid().ToString(); Url = Uri newUrl } }
        
        let withNewTitle (newTitle: string) =
            this.Deviation <- { this.Deviation with Title = newTitle }
            
        let withNewIsMature (newIsMature: bool) =
            this.Deviation <- { this.Deviation with IsMature = newIsMature }
        
        let withNewGallery (newGallery: string) =
            
            this.Deviation <- {
                this.Deviation with
                    Gallery = newGallery
                    IsMature =
                        match newGallery with
                        | "Spicy" -> true
                        | _ -> this.Deviation.IsMature
            }
        
        comp<RadzenStack> {
            attr.style "margin: 0.25rem; padding: 0.5rem; border: 2px solid gray; border-radius: 8px; max-width: 700px;"

            "Orientation" => Orientation.Horizontal
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center

            comp<ImagePreview> {
                "Image" => this.Image
            }

            comp<RadzenStack> {
                "Orientation" => Orientation.Vertical

                div { text $"{this.Deviation.Id}" }

                this.Deviation.Inspiration
                |> Option.map _.Url.ToString()
                |> Option.defaultValue ""
                |> TextInput.render withNewInspiration "Enter inspiration URL..."

                this.Deviation.Title
                |> TextInput.render withNewTitle "Enter title..."

                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    
                    this.Deviation.Gallery
                    |> DropDown.render withNewGallery "Gallery" "Select gallery..." this.Galleries
                    
                    this.Deviation.IsMature
                    |> CheckBox.render withNewIsMature "IsMature"
                }
                
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    "JustifyContent" => JustifyContent.Center
                    "AlignItems" => AlignItems.Center

                    Button.render this (fun () -> this.OnSave this.Deviation) "Save"
                    Button.render this (fun () -> this.OnStash this.Deviation) "Stash"
                }
            }
        }
