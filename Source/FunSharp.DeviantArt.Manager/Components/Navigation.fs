namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero.Html
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Manager.Model
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Navigation =
    
    let render parent (nav: NavigationManager) (model: State) =
        
        let inspirationsCount =
            match model.Inspirations with
            | Loaded inspirations -> inspirations.Length
            | _ -> -1
        
        let promptsCount =
            match model.Prompts with
            | Loaded prompts -> prompts.Length
            | _ -> -1
        
        let localDeviationsCount =
            match model.LocalDeviations with
            | Loaded deviations -> deviations.Length
            | _ -> -1
            
        let stashedDeviationsCount =
            match model.StashedDeviations with
            | Loaded deviations -> deviations.Length
            | _ -> -1
            
        let publishedDeviationsCount =
            match model.PublishedDeviations with
            | Loaded deviations -> deviations.Length
            | _ -> -1
        
        let currentLocation = (Uri nav.Uri).AbsolutePath
        
        let navigateTo url =
            nav.NavigateTo(url, false, true)
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            
            [
                ("/inspirations", $"Inspirations ({inspirationsCount})")
                ("/prompts", $"Prompts ({promptsCount})")
                ("/local-deviations", $"Local Deviations ({localDeviationsCount})")
                ("/stashed-deviations", $"Stashed Deviations ({stashedDeviationsCount})")
                ("/published-deviations", $"Published Deviations ({publishedDeviationsCount})")
            ]
            |> List.map (fun (endpoint, label) ->
                let disabled =
                    endpoint = currentLocation

                Button.render parent (fun () -> navigateTo(endpoint)) disabled label
            )
            |> Helpers.renderList
        }
