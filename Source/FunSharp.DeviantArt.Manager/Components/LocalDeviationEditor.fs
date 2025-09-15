namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Api.Model

type LocalDeviationEditor() =
    inherit Component()
    
    [<Parameter>]
    member val Galleries = Array.empty<string> with get, set
    
    [<Parameter>]
    member val Deviation : LocalDeviation option = None with get, set

    [<Parameter>]
    member val OnSave : LocalDeviation -> unit = ignore with get, set

    [<Parameter>]
    member val OnStash : LocalDeviation -> unit = ignore with get, set

    [<Parameter>]
    member val OnDelete : LocalDeviation -> unit = ignore with get, set
    
    member private this.Update(withChange: LocalDeviation -> LocalDeviation) =
        this.Deviation <-
            this.Deviation
            |> Option.map withChange
    
    member private this.Save() =
        match this.Deviation with
        | None -> ()
        | Some v -> this.OnSave v
        
    member private this.Stash() =
        match this.Deviation with
        | None -> ()
        | Some v -> this.OnStash v
        
    member private this.Delete() =
        match this.Deviation with
        | None -> ()
        | Some v -> this.OnDelete v
    
    override this.Render() =
            
        let withNewInspiration (newUrl: string) =
            // let newInspiration = { Url = Uri newUrl; Timestamp = DateTimeOffset.Now; ImageUrl = None }
            let newInspiration = { Url = Uri newUrl; ImageUrl = None }
            this.Update(fun x -> { x with Origin = DeviationOrigin.Inspiration newInspiration })
            
        let withNewTitle (newTitle: string) =
            this.Update(fun x -> { x with LocalDeviation.Metadata.Title = newTitle })
            
        let withNewIsMature (newIsMature: bool) =
            this.Update(fun x -> { x with LocalDeviation.Metadata.IsMature = newIsMature })
            
        let withNewGallery (newGallery: string) =
            this.Update(fun x -> {
                x with
                    LocalDeviation.Metadata.Gallery = newGallery
                    LocalDeviation.Metadata.IsMature =
                        match newGallery with
                        | "Spicy" -> true
                        | _ -> x.Metadata.IsMature
            })
            
        concat {
            this.Deviation
            |> Option.map _.ImageUrl
            |> ImageUrl.render
            
            match this.Deviation |> Option.map _.Origin |> Option.defaultValue DeviationOrigin.None with
            | DeviationOrigin.None ->
                "" |> TextInput.render withNewInspiration (fun _ -> ()) "Enter inspiration URL..."
            | DeviationOrigin.Inspiration inspiration ->
                inspiration.Url.ToString() |> TextInput.render withNewInspiration (fun _ -> ()) "Enter inspiration URL..."
            | DeviationOrigin.Prompt prompt ->
                match prompt.Inspiration with
                | None -> ()
                | Some inspiration ->
                    inspiration.Url |> Link.render None
            
            this.Deviation |> Option.map _.Metadata.Title |> Option.defaultValue ""
            |> TextInput.render withNewTitle (fun _ -> ()) "Enter title..."

            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                
                this.Deviation |> Option.map _.Metadata.Gallery |> Option.defaultValue ""
                |> DropDown.render withNewGallery "Gallery" "Select gallery..." this.Galleries
                
                this.Deviation |> Option.map _.Metadata.IsMature |> Option.defaultValue false
                |> CheckBox.render withNewIsMature "IsMature"
            }
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "JustifyContent" => JustifyContent.Center
                "AlignItems" => AlignItems.Center

                Button.render this this.Delete "Delete"
                Button.render this this.Save "Save"
                Button.render this this.Stash "Stash"
            }
        }
        |> Deviation.renderWithContent (this.Deviation |> Option.map _.ImageUrl)
