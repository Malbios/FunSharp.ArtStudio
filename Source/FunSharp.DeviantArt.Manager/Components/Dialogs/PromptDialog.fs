namespace FunSharp.DeviantArt.Manager.Components

open System
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
    
    let mutable prompt = Array.empty<string>
    
    let cleanedParagraphs (promptText: string) =
        
        promptText.Split([| "\r\n" |], StringSplitOptions.None)
        |> Array.filter (fun s -> not (String.IsNullOrWhiteSpace s))
        |> Array.map _.Trim()

    let clipboardSnippetAction (snippet: ClipboardSnippet) =
        
        match snippet.action with
        | Append paragraph ->
            match paragraph with
            | First -> prompt[0] <- prompt[0] + snippet.value
            | Last -> prompt[prompt.Length - 1] <- prompt[prompt.Length - 1] + snippet.value
            | Index i -> prompt[i] <- prompt[i] + snippet.value
            
        | Replace paragraph ->
            match paragraph with
            | First -> prompt[0] <- snippet.value
            | Last -> prompt[prompt.Length - 1] <- snippet.value
            | Index i -> prompt[i] <- snippet.value

    [<Inject>]
    member val DialogService = Unchecked.defaultof<DialogService> with get, set
    
    [<Parameter>]
    member val Snippets : ClipboardSnippet array = Array.empty with get, set
    
    [<Parameter>]
    member this.Prompt
        with get() =
            prompt |> String.concat "\n\n"
        and set (value: string) =
            prompt <- cleanedParagraphs value
        
    override this.Render() =
        
        let snippetButton snippet =
            Button.render snippet.label (fun () -> clipboardSnippetAction snippet) false
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            
            div {
                attr.style "padding: 0.5rem;"
                
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    "Gap" => "1rem"
                    
                    this.Snippets
                    |> Array.map snippetButton
                    |> Helpers.renderArray
                }
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
            | Loadable.Loaded settings -> settings.Snippets
            | _ -> Array.empty
        
        let parameters = dict [
            "Snippets", box snippets
            "Prompt", box currentPrompt
        ]
        
        let parameters = Dictionary<string, obj>(parameters)
        
        dialogService.OpenAsync<PromptDialog>(title, parameters)

    static member OpenAsync(dialogService: DialogService, settings: Loadable<Settings>, title: string) =
        
        PromptDialog.OpenAsync(dialogService, settings, title, "")
        
