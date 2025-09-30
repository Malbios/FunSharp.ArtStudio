namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero.Html
open FunSharp.Blazor.Components
open FunSharp.Common
open FunSharp.DeviantArt.Manager.Model
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Navigation =
    
    let render (model: State) dispatch (nav: NavigationManager) (dialogService: DialogService) =
        
        let inspirationsCount =
            match model.Inspirations with
            | Loadable.Loaded inspirations -> inspirations.Length
            | _ -> -1
        
        let promptsCount =
            match model.Prompts with
            | Loadable.Loaded prompts -> prompts.Length
            | _ -> -1
        
        let localDeviationsCount =
            match model.LocalDeviations with
            | Loadable.Loaded deviations -> deviations.total
            | _ -> -1
            
        let stashedDeviationsCount =
            match model.StashedDeviations with
            | Loadable.Loaded deviations -> deviations.Length
            | _ -> -1
            
        let publishedDeviationsCount =
            match model.PublishedDeviations with
            | Loadable.Loaded deviations -> deviations.Length
            | _ -> -1
        
        let currentLocation = (Uri nav.Uri).AbsolutePath
        
        let navigateTo url =
            nav.NavigateTo(url, false, true)
            
        let reloadCurrent, reloadCurrentDisabled =
            match currentLocation with
            | "/add-inspiration" -> (fun () -> ()), true
            | "/inspirations" -> (fun () -> dispatch LoadInspirations), false
            | "/prompts" -> (fun () -> dispatch LoadPrompts), false
            | "/local-deviations" -> (fun () -> dispatch LoadLocalDeviations), false
            | "/stashed-deviations" -> (fun () -> dispatch LoadStashedDeviations), false
            | "/published-deviations" -> (fun () -> dispatch LoadPublishedDeviations), false
            | other ->
                printfn $"ERROR: unexpected endpoint: {other}"
                (fun () -> ()), true
                
        let openNewPromptDialog () =
            
            PromptDialog.OpenAsync(dialogService, model.Settings, "New Prompt")
            |> Task.map (function
                | :? string as promptText -> Message.AddPrompt promptText |> dispatch
                | _ -> ()
            )
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            
            Button.renderAsync "Add Prompt" (fun () -> openNewPromptDialog ()) false
            
            [
                ("/add-inspiration", $"Add Inspiration")
                ("/inspirations", $"Inspirations ({inspirationsCount})")
                ("/prompts", $"Prompts ({promptsCount})")
                ("/local-deviations", $"Local Deviations ({localDeviationsCount})")
                ("/stashed-deviations", $"Stashed Deviations ({stashedDeviationsCount})")
                ("/published-deviations", $"Published Deviations ({publishedDeviationsCount})")
            ]
            |> List.map (fun (endpoint, label) ->
                let disabled =
                    endpoint = currentLocation

                Button.render label (fun () -> navigateTo(endpoint)) disabled
            )
            |> Helpers.renderList
            
            div {
                attr.style "margin-left: 2rem;"
                Button.render "Reload This" reloadCurrent reloadCurrentDisabled
            }
            
            div {
                attr.style "margin-left: 0.25rem;"
                Button.render "Reload All" (fun () -> dispatch LoadAll) false
            }
        }
