namespace FunSharp.DeviantArt.Manager.Components

open System.Collections.Generic
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Model
open Microsoft.AspNetCore.Components
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor

type PromptDialog() =
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
                attr.style "padding: 0.5rem;"
                ClipboardSnippets.render this.JSRuntime this.Snippets
            }
            
            TextAreaInput.render 20 200 (fun s -> promptText <- s) "Enter prompt..." promptText
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                
                Button.render "Ok" (fun () -> this.DialogService.Close(promptText)) false
                Button.render "Cancel" (fun () -> this.DialogService.Close(null)) false
            }
        }

    static member OpenAsync(dialogService: DialogService, settings: Loadable<Settings>, title: string) =
        
        let snippets =
            match settings with
            | Loaded settings -> settings.Snippets
            | _ -> Array.empty
        
        let parameters = Dictionary<string, obj>(dict [ "Snippets", box snippets ])
        
        dialogService.OpenAsync<PromptDialog>(title, parameters)
