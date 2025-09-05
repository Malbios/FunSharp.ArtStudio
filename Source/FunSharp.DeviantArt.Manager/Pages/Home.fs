namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Bolero.Html.attr
open Microsoft.AspNetCore.Components.Forms
open Radzen
open Radzen.Blazor
open FunSharp.DeviantArt.Manager.Components
open FunSharp.DeviantArt.Manager.Model

type Home() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.Home
    
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
                
                comp<LocalDeviationsEditor> {
                    "Galleries" => galleries
                    "Items" => model.LocalDeviations
                    "OnSave" => (fun x -> dispatch (Message.UpdateLocalDeviation x))
                    "OnStash" => (fun x -> dispatch (Message.StashDeviation x))
                }
                
                // for file in model.StashedDeviations do
                //     
                //     let inspiration = file.Metadata.Inspiration |> Option.map _.ToString() |> Option.defaultValue ""
                //     
                //     div {
                //         style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
                //         
                //         a { href $"{Helpers.stashUrl file.StashId}" }
                //         text $"Inspired by {inspiration}"
                //     }
            }
        }
