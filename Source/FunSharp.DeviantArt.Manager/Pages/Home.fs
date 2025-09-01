namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Radzen.Blazor

type Home() =
    inherit Component()
    
    override _.CssScope = CssScopes.MyApp

    override this.Render() =
        
        div {
            attr.``class`` "center-wrapper"
            
            comp<RadzenStack> {
                attr.style "height: 100%"

                "JustifyContent" => Radzen.JustifyContent.Center
                "AlignItems" => Radzen.AlignItems.Center

                comp<RadzenProgressBarCircular> {
                    "ShowValue" => false
                    "Mode" => Radzen.ProgressBarMode.Indeterminate
                }
                
                // TODO: show actual content
            }
        }
