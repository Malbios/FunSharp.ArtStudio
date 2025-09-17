namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero.Html
open FunSharp.Blazor.Components
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Navigation =
    
    let render parent (nav: NavigationManager) =
        
        let currentLocation = (Uri nav.Uri).AbsolutePath
        
        let navigateTo url =
            nav.NavigateTo(url, false, true)
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            
            [
                ("/inspirations", "Inspirations")
                ("/prompts", "Prompts")
                ("/local-deviations", "Local Deviations")
                ("/stashed-deviations", "Stashed Deviations")
                ("/published-deviations", "Published Deviations")
            ]
            |> List.map (fun (endpoint, label) ->
                let disabled =
                    endpoint = currentLocation

                Button.render parent (fun () -> navigateTo(endpoint)) disabled label
            )
            |> Helpers.renderList
        }
