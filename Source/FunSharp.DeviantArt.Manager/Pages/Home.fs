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

type Home() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.Home
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<IJSRuntime> with get, set
    
    override this.View model dispatch =
        
        let galleries =
            match model.Settings with
            | Loaded settings -> settings.Galleries |> Array.map _.name
            | _ -> Array.empty
    
        let uploadFiles (args: InputFileChangeEventArgs) =
            args.GetMultipleFiles ()
            |> Array.ofSeq
            |> Message.ProcessImages
            |> dispatch
        
        div {
            attr.``class`` "center-wrapper"
            
            comp<RadzenStack> {
                style "height: 100%"

                "Orientation" => Orientation.Vertical
                "JustifyContent" => JustifyContent.Center
                "AlignItems" => AlignItems.Center
                
                FileInput.render true uploadFiles
                
                comp<LocalDeviations> {
                    "Galleries" => galleries
                    "Images" => model.Images
                    "Items" => model.LocalDeviations
                    "OnSave" => (fun x -> dispatch (Message.UpdateLocalDeviation x))
                    "OnStash" => (fun x -> dispatch (Message.StashDeviation x))
                }
                
                model.StashedDeviations
                |> StashedDeviations.render this this.JSRuntime (fun deviation -> dispatch (Message.PublishStashed deviation)) model.Images
                
                model.PublishedDeviations
                |> PublishedDeviations.render model.Images
            }
        }
