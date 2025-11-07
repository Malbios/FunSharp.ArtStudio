namespace FunSharp.ArtStudio.Client.Components

open System
open Bolero.Html
open FunSharp.Blazor.Components
open FunSharp.Common
open FunSharp.ArtStudio.Client.Model
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Navigation =
    
    let private renderInternal (model: State) dispatch (nav: NavigationManager) (dialogService: DialogService) =
        
        let inspirationsCount =
            match model.Inspirations with
            | Loadable.Loaded inspirations -> inspirations.Length
            | _ -> -1
        
        let promptsCount =
            match model.Prompts with
            | Loadable.Loaded prompts -> prompts.Length
            | _ -> -1
        
        let soraTasksCount =
            match model.SoraTasks with
            | Loadable.Loaded soraTasks -> soraTasks.Length
            | _ -> -1
        
        let soraResultsCount =
            match model.SoraResults with
            | Loadable.Loaded soraResults -> soraResults.Length
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
            
        let reloadCurrent =
            match currentLocation with
            | "/add-inspiration" -> fun () -> ()
            | "/inspirations" -> fun () -> dispatch LoadInspirations
            | "/prompts" -> fun () -> dispatch LoadPrompts
            | "/sora" -> fun () -> dispatch LoadSoraTasks; dispatch LoadSoraResults
            | "/local-deviations" -> fun () -> dispatch LoadLocalDeviations
            | "/stashed-deviations" -> fun () -> dispatch LoadStashedDeviations
            | "/published-deviations" -> fun () -> dispatch LoadPublishedDeviations
            | other ->
                printfn $"ERROR: unexpected endpoint: {other}"
                fun () -> ()
                
        let openNewPromptDialog () =
            
            PromptDialog.OpenAsync(dialogService, model.Settings, "New Prompt")
            |> Task.map (function
                | :? PromptDialogResult as result ->
                    match result with
                    | PromptDialogResult.Prompt promptText ->
                        Message.AddPrompt promptText |> dispatch
                    | PromptDialogResult.SoraTask (promptText, aspectRatio) ->
                        Message.NewPrompt2SoraTask (promptText, aspectRatio) |> dispatch
                | _ -> ()
            )
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            "Gap" => "2rem"
            
            Button.renderSimpleAsync "Add Prompt" <| fun () -> openNewPromptDialog ()
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "Gap" => "0.5rem"
                
                [
                    ("/add-inspiration", $"Add Inspiration")
                    ("/inspirations", $"Inspirations ({inspirationsCount})")
                    ("/prompts", $"Prompts ({promptsCount})")
                    ("/sora", $"Sora ({soraTasksCount},{soraResultsCount})")
                    ("/local-deviations", $"Local Deviations ({localDeviationsCount})")
                    ("/stashed-deviations", $"Stashed Deviations ({stashedDeviationsCount})")
                    ("/published-deviations", $"Published Deviations ({publishedDeviationsCount})")
                ]
                |> List.map (fun (endpoint, label) ->
                    Button.render <| {
                        ButtonProps.defaults with
                            Text = label
                            ButtonStyle = ButtonStyle.Base
                            Variant = Variant.Outlined
                            Action = ClickAction.Sync <| fun () -> navigateTo(endpoint)
                            Disabled = endpoint = currentLocation
                    }
                )
                |> Helpers.renderList
            }
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "Gap" => "0.25rem"
                
                Button.renderSimple "Reload This" <| reloadCurrent
                Button.renderSimple "Reload All" <| fun () -> dispatch LoadAll
            }
        }
        
    let render (model: State) dispatch =
        
        fun (nav: NavigationManager) ->
            fun (dialogService: DialogService) ->
                renderInternal model dispatch nav dialogService
            |> Injector.withInjected<DialogService>
        |> Injector.withInjected<NavigationManager>
