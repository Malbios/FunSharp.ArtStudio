namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open FunSharp.DeviantArt.Manager.Components
open FunSharp.DeviantArt.Manager.Model
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor

type NotFound() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.``FunSharp.DeviantArt.Manager``
    
    [<Inject>]
    member val NavManager: NavigationManager = Unchecked.defaultof<_> with get, set
    
    [<Inject>]
    member val DialogService = Unchecked.defaultof<DialogService> with get, set
    
    override this.View model dispatch =
        
        comp<RadzenStack> {
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center
            
            p { "404 - Not Found" }
        }
        |> Page.render model dispatch this.NavManager this.DialogService
