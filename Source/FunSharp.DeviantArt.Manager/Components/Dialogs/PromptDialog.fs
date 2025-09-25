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
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<_> with get, set

    [<Inject>]
    member val DialogService = Unchecked.defaultof<DialogService> with get, set
    
    [<Parameter>]
    member val Snippets : ClipboardSnippet array = Array.empty with get, set
    
    [<Parameter>]
    member val Prompt = "" with get, set

    override this.Render() =
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            
            div {
                attr.style "padding: 0.5rem;"
                ClipboardSnippets.render this.JSRuntime this.Snippets
            }
            
            TextAreaInput.render 20 200 (fun s -> this.Prompt <- s) "Enter prompt..." this.Prompt
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                
                Button.render "Ok" (fun () -> this.DialogService.Close(this.Prompt)) false
                Button.render "Cancel" (fun () -> this.DialogService.Close(null)) false
            }
        }

    static member OpenAsync(dialogService: DialogService, settings: Loadable<Settings>, title: string, currentPrompt: string) =
        
        let snippets =
            match settings with
            | Loaded settings -> settings.Snippets
            | _ -> Array.empty
        
        let parameters = dict [
            "Snippets", box snippets
            "Prompt", box currentPrompt
        ]
        
        let parameters = Dictionary<string, obj>(parameters)
        
        dialogService.OpenAsync<PromptDialog>(title, parameters)

    static member OpenAsync(dialogService: DialogService, settings: Loadable<Settings>, title: string) =
        
        PromptDialog.OpenAsync(dialogService, settings, title, "")
