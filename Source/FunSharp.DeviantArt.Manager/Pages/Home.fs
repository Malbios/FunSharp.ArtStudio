namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Bolero.Html.attr
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Forms
open Microsoft.JSInterop
open Radzen
open Radzen.Blazor
open FunSharp.DeviantArt.Manager.Components
open FunSharp.DeviantArt.Manager.Model

type HomeTabs =
    | UploadFiles
    | LocalDeviations
    | StashedDeviations
    | PublishedDeviations

type Home() =
    inherit ElmishComponent<State, Message>()
    
    let mutable currentTab = HomeTabs.LocalDeviations
    
    override _.CssScope = CssScopes.Home
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<IJSRuntime> with get, set
    
    override this.View model dispatch =
            
        let loadImage imageId =
            dispatch (Message.LoadImage imageId)
        
        let galleries =
            match model.Settings with
            | Loaded settings -> settings.Galleries |> Array.map _.name
            | _ -> Array.empty
    
        let uploadFiles (args: InputFileChangeEventArgs) =
            args.GetMultipleFiles ()
            |> Array.ofSeq
            |> Message.ProcessImages
            |> dispatch
            
        let publish deviation =
            dispatch (Message.PublishStashed deviation)
        
        div {
            attr.``class`` "center-wrapper"
            
            comp<RadzenStack> {
                style "height: 100%"
            
                "Orientation" => Orientation.Vertical
                "JustifyContent" => JustifyContent.Center
                "AlignItems" => AlignItems.Center
                
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    "JustifyContent" => JustifyContent.Center
                    "AlignItems" => AlignItems.Center
                    
                    Button.render this (fun () -> currentTab <- HomeTabs.UploadFiles) "Upload Files"
                    Button.render this (fun () -> currentTab <- HomeTabs.LocalDeviations) "Local Deviations"
                    Button.render this (fun () -> currentTab <- HomeTabs.StashedDeviations) "Stashed Deviations"
                    Button.render this (fun () -> currentTab <- HomeTabs.PublishedDeviations) "Published Deviations"
                }
                
                match currentTab with
                | UploadFiles ->
                    FileInput.render true uploadFiles
                    
                | LocalDeviations ->
                    comp<LocalDeviations> {
                        "LoadImage" => loadImage
                        "Galleries" => galleries
                        "Images" => model.Images
                        "Items" => model.LocalDeviations
                        "OnSave" => (fun deviation -> dispatch (Message.UpdateLocalDeviation deviation))
                        "OnStash" => (fun deviation -> dispatch (Message.StashDeviation deviation))
                    }
                    
                | StashedDeviations ->
                    model.StashedDeviations
                    |> StashedDeviations.render this this.JSRuntime publish loadImage model.Images
                    
                | PublishedDeviations ->
                    model.PublishedDeviations
                    |> PublishedDeviations.render loadImage model.Images
            }
        }
