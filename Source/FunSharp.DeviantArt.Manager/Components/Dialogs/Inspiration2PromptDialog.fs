namespace FunSharp.DeviantArt.Manager.Components

open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Model
open Microsoft.AspNetCore.Components
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor

type Inspiration2PromptDialog() =
    inherit Component()
    
    let mutable promptText = ""
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<_> with get, set

    [<Inject>]
    member val DialogService = Unchecked.defaultof<DialogService> with get, set
    
    [<Parameter>]
    member val Snippets : ClipboardSnippet array = Array.empty with get, set

    override this.Render() =
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            
            div {
                attr.style "padding: 0.5rem; border: 2px solid gray; border-radius: 8px;"
                ClipboardSnippets.render this.JSRuntime this.Snippets
            }
            
            TextAreaInput.render 10 50 (fun s -> promptText <- s) "Enter prompt..." promptText
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                
                Button.render "Ok" (fun () -> this.DialogService.Close(promptText)) false
                Button.render "Cancel" (fun () -> this.DialogService.Close(null)) false
            }
        }
