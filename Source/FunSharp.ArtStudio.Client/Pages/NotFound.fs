namespace FunSharp.ArtStudio.Client.Pages

open Bolero
open Bolero.Html
open FunSharp.ArtStudio.Client.Components
open FunSharp.ArtStudio.Client.Model
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor

type NotFound() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.``FunSharp.ArtStudio.Client``
    
    override this.View model dispatch =
        
        comp<RadzenStack> {
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center
            
            p { "404 - Not Found" }
        }
        |> Page.render model dispatch
