namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Model
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.Components

type LocalDeviations() =
    inherit ElmishComponent<State, Message>()
            
    let galleries model =
        match model.Settings with
        | Loaded settings -> settings.Galleries |> Array.map _.name
        | _ -> Array.empty
        
    let originLink origin =
        
        match origin with
        | DeviationOrigin.None -> Node.Empty ()
        | DeviationOrigin.Inspiration inspiration ->
            Link.render None inspiration.Url
        | DeviationOrigin.Prompt prompt ->
            match prompt.Inspiration with
            | None -> Node.Empty ()
            | Some inspiration ->
                Link.render None inspiration.Url
    
    let editorWidget dispatch galleries deviation =
        
        let update deviation =
            
            Message.UpdateLocalDeviation deviation |> dispatch
            
        let stash deviation =
            
            Message.StashDeviation deviation |> dispatch
            
        let forget deviation =
            
            Message.ForgetLocalDeviation deviation |> dispatch
            
        let editTitle deviation newTitle =
            
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
            
            TextInput.render (editTitle deviation) (fun _ -> ()) false "Enter title..." deviation.Metadata.Title
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                
                DropDown.render (editGallery deviation) "Gallery" "Select gallery..." galleries deviation.Metadata.Gallery
                CheckBox.render (editIsMature deviation) "IsMature" deviation.Metadata.IsMature
            }
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                
                Button.render "Stash" (fun () -> stash deviation) false
                Button.render "Forget" (fun () -> forget deviation) false
            }
        }
    
    override _.CssScope = CssScopes.LocalDeviations
    
    [<Inject>]
    member val NavManager: NavigationManager = Unchecked.defaultof<_> with get, set
    
    override this.View model dispatch =
        
        let galleries = galleries model
        
        let localDeviationsCount =
            match model.LocalDeviations with
            | Loaded deviations -> deviations.Length
            | _ -> -1

        match localDeviationsCount with
        | 0 -> text "No items."
        | _ ->
            Loadable.render model.LocalDeviations
            <| fun deviations ->
                Deviations.render
                <| concat {
                    for deviation in deviations |> StatefulItemArray.sortBy _.Timestamp do
                        match deviation with
                        | IsBusy _ ->
                            LoadingWidget.render ()
                            
                        | HasError (deviation, error) ->
                            concat {
                                text $"deviation: {LocalDeviation.keyOf deviation}"
                                text $"error: {error}"
                            }
                            
                        | Default deviation ->
                            let inspirationUrl =
                                match deviation.Origin with
                                | DeviationOrigin.None -> None
                                | DeviationOrigin.Prompt prompt -> prompt.Inspiration |> Option.bind _.ImageUrl
                                | DeviationOrigin.Inspiration inspiration -> inspiration.ImageUrl

                            comp<RadzenStack> {
                                "Orientation" => Orientation.Vertical
                                "JustifyContent" => JustifyContent.Left
                                
                                deviation.Timestamp.ToString() |> text
                                
                                ImageUrl.render (Some deviation.ImageUrl)
                                
                                originLink deviation.Origin
                                
                                editorWidget dispatch galleries deviation
                            }
                            |> Deviation.renderWithContent inspirationUrl (Some deviation.ImageUrl)
                }
        |> Page.render model dispatch this.NavManager
