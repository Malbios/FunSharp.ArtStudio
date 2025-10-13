namespace FunSharp.ArtStudio.Client.Pages

open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.ArtStudio.Model
open FunSharp.ArtStudio.Client.Model
open FunSharp.ArtStudio.Client.Components

[<RequireQualifiedAccess>]
module LocalDeviations =
            
    let galleries model =
        
        match model.Settings with
        | Loadable.Loaded settings ->
            settings.Galleries |> Array.map _.name
        | _ -> Array.empty
        
    let originLink origin =
        
        match DeviationOrigin.inspiration origin with
        | None -> Node.Empty ()
        | Some inspiration ->
            Link.renderSimple None inspiration.Url
    
    let editorWidget dispatch js galleries isBusy deviation =
        
        let update deviation =
            
            dispatch <| Message.UpdateLocalDeviation deviation
            
        let stash deviation =
            
            dispatch <| Message.StashDeviation deviation
            
        let forget deviation =
            
            dispatch <| Message.ForgetLocalDeviation deviation
            
        let editTitle deviation newTitle =
            
            if deviation.Metadata.Title <> newTitle then
                { deviation with LocalDeviation.Metadata.Title = newTitle }
                |> update
            
        let editGallery deviation newGallery =
            
            let isMature =
                match newGallery with
                | "Spicy" -> true
                | _ -> deviation.Metadata.IsMature
                
            {
                deviation with
                    LocalDeviation.Metadata.Gallery = newGallery
                    LocalDeviation.Metadata.IsMature = isMature
            }
            |> update
            
        let editIsMature deviation newIsMature =
            
            { deviation with LocalDeviation.Metadata.IsMature = newIsMature }
            |> update
            
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            
            TextInput.render (editTitle deviation) (fun _ -> ()) isBusy "Enter title..." deviation.Metadata.Title
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                
                DropDown.render (editGallery deviation) "Gallery" isBusy "Select gallery..." galleries deviation.Metadata.Gallery
                CheckBox.render (editIsMature deviation) "IsMature" isBusy deviation.Metadata.IsMature
            }
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "JustifyContent" => JustifyContent.SpaceBetween
                
                Button.render "Stash" (fun () -> stash deviation) isBusy
                
                deviation
                |> _.Origin
                |> DeviationOrigin.prompt
                |> function
                    | None -> Node.Empty()
                    | Some prompt ->
                        Button.render "Copy Prompt" (fun () -> Helpers.copyToClipboard js prompt.Text) false
                        
                Button.render "Forget" (fun () -> forget deviation) isBusy
            }
        }
        
    let deviationWidget dispatch js galleries deviation =
        let deviation, isBusy, error =
            match deviation with
            | StatefulItem.Default deviation -> deviation, false, None
            | StatefulItem.IsBusy deviation -> deviation, true, None
            | StatefulItem.HasError (deviation, error) -> deviation, false, Some error
            
        let inspirationUrl =
            match deviation.Origin with
            | DeviationOrigin.None -> None
            | DeviationOrigin.Prompt prompt -> prompt.Inspiration |> Option.bind _.ImageUrl
            | DeviationOrigin.Inspiration inspiration -> inspiration.ImageUrl
        
        match isBusy with
        | true -> Node.Empty()
        
        | false ->
            comp<RadzenStack> {
                "Orientation" => Orientation.Vertical
                "JustifyContent" => JustifyContent.Left
                
                deviation.Timestamp.ToString() |> text
                
                ImageUrl.render (Some deviation.ImageUrl)
                
                originLink deviation.Origin
                
                match error with
                | None -> ()
                | Some error ->
                    concat {
                        text $"deviation: {LocalDeviation.keyOf deviation}"
                        text $"error: {error}"
                    }
                
                editorWidget dispatch js galleries isBusy deviation
            }
            |> Deviation.renderWithContent inspirationUrl (Some deviation.ImageUrl)

type LocalDeviations() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.LocalDeviations
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<_> with get, set
    
    override this.View model dispatch =
        
        let pageSize = FunSharp.ArtStudio.Client.Helpers.localDeviationsPageSize
        
        let changePage newPage =
            Message.LoadLocalDeviationsPage (newPage * pageSize, pageSize) |> dispatch
        
        let localDeviationsCount =
            match model.LocalDeviations with
            | Loadable.Loaded page -> page.total
            | _ -> -1

        match localDeviationsCount with
        | 0 -> text "No items."
        | _ ->
            Loadable.render model.LocalDeviations
            <| fun page ->
                comp<RadzenStack> {
                    "Orientation" => Orientation.Vertical
                    "AlignItems" => AlignItems.Center
                    "Gap" => "3rem"
                    
                    let galleries = LocalDeviations.galleries model
                    
                    page.items
                    |> Array.map (LocalDeviations.deviationWidget dispatch this.JSRuntime galleries)
                    |> Helpers.renderArray
                    |> Deviations.render
                    
                    Pager.render page.total pageSize page.offset changePage
                }
        |> Page.render model dispatch
