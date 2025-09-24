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
    
    override _.CssScope = CssScopes.LocalDeviations
    
    [<Inject>]
    member val NavManager: NavigationManager = Unchecked.defaultof<_> with get, set
    
    override this.View model dispatch =
            
        let galleries =
            match model.Settings with
            | Loaded settings -> settings.Galleries |> Array.map _.name
            | _ -> Array.empty
        
        let fieldConfigurations =
            let metadataConfigurations (onChange: obj -> unit) (currentValue: obj) =
                let currentMetadata = currentValue :?> Metadata
                
                let onNewTitle (newTitle: string) =
                    { currentMetadata with Title = newTitle } |> onChange
                    
                let onNewGallery (newGallery: string) =
                    {
                        currentMetadata with
                            Gallery = newGallery
                            IsMature =
                                match newGallery with
                                | "Spicy" -> true
                                | _ -> currentMetadata.IsMature
                    } |> onChange
                    
                let onNewIsMature (newIsMature: bool) =
                    { currentMetadata with IsMature = newIsMature } |> onChange
                
                comp<RadzenStack> {
                    "Orientation" => Orientation.Vertical
                    
                    TextInput.render onNewTitle (fun _ -> ()) false "Enter title..." currentMetadata.Title
                    
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Horizontal
                        
                        DropDown.render onNewGallery "Gallery" "Select gallery..." galleries currentMetadata.Gallery
                        
                        CheckBox.render onNewIsMature "IsMature" currentMetadata.IsMature
                    }
                }
                
            [
                ("Metadata", metadataConfigurations)
            ]
            |> Map.ofList
        
        let localDeviationsCount =
            match model.LocalDeviations with
            | Loaded deviations -> deviations.Length
            | _ -> -1
            
        let updateLocalDeviation deviation =
            Message.UpdateLocalDeviation deviation |> dispatch
            
        let stashDeviation deviation =
            Message.StashDeviation deviation |> dispatch
            
        let forgetDeviation deviation =
            Message.ForgetLocalDeviation deviation |> dispatch

        match localDeviationsCount with
        | 0 -> text "No items."
        | _ ->
            Loadable.render model.LocalDeviations
            <| fun deviations ->
                Deviations.render
                <| concat {
                    for deviation in deviations |> StatefulItemArray.sortBy _.Timestamp do
                        let deviation = StatefulItem.valueOf deviation
                        
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
                            
                            match deviation.Origin with
                            | DeviationOrigin.None -> ()
                            | DeviationOrigin.Inspiration inspiration ->
                                Link.render None inspiration.Url
                            | DeviationOrigin.Prompt prompt ->
                                match prompt.Inspiration with
                                | None -> ()
                                | Some inspiration ->
                                    Link.render None inspiration.Url

                            comp<ItemEditor<LocalDeviation>> {
                                "Fields" => fieldConfigurations
                                "Item" => Some deviation
                                "OnSave" => updateLocalDeviation
                                "FinishLabel" => "Stash"
                                "OnFinish" => stashDeviation
                                "OnForget" => forgetDeviation
                            }
                        }
                        |> Deviation.renderWithContent inspirationUrl (Some deviation.ImageUrl)
                }
        |> Page.render model dispatch this.NavManager
