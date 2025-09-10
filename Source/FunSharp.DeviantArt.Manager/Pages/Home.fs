namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Bolero.Html.attr
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Forms
open Microsoft.JSInterop
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.Components

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
            |> Message.AddLocalDeviations
            |> dispatch
            
        let publish deviation =
            dispatch (Message.PublishStashed deviation)
            
        let accordionItem label expanded renderAction : Accordion.Item = {
            Label = label
            Expanded = expanded
            RenderAction = renderAction
        }
        
        div {
            attr.``class`` "center-wrapper"
            
            div {
                style "height: 100%"
                
                [|
                    accordionItem "Upload Images" true (fun () ->
                        FileInput.render true uploadFiles
                    )
                    
                    accordionItem "Local Deviations" true (fun () ->
                        comp<LocalDeviations> {
                            "Galleries" => galleries
                            "Items" => model.LocalDeviations
                            "OnSave" => (fun deviation -> dispatch (Message.UpdateLocalDeviation deviation))
                            "OnStash" => (fun deviation -> dispatch (Message.StashDeviation deviation))
                        }
                    )
                    
                    accordionItem "Stashed Deviations" true (fun () ->
                        model.StashedDeviations
                        |> StashedDeviations.render this this.JSRuntime publish
                    )
                    
                    accordionItem "Published Deviations" false (fun () ->
                        model.PublishedDeviations
                        |> PublishedDeviations.render
                    )
                |]
                |> Accordion.render true
            }
        }
