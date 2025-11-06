namespace FunSharp.ArtStudio.Client.Components

open System
open System.Collections.Generic
open FunSharp.OpenAI.Api.Model.Sora
open Microsoft.AspNetCore.Components
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.ArtStudio.Model
open FunSharp.ArtStudio.Client.Model

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
            
            Button.renderSimple snippet.label <| fun () -> clipboardSnippetAction snippet
        
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
                
                let landscape = PromptDialogResult.Inspiration2SoraTask (this.Prompt, AspectRatio.Landscape)
                let square = PromptDialogResult.Inspiration2SoraTask (this.Prompt, AspectRatio.Square)
                let portrait = PromptDialogResult.Inspiration2SoraTask (this.Prompt, AspectRatio.Portrait)
                
                Button.renderSimple "Portrait (2:3)" <| fun () -> this.DialogService.Close(portrait)
                Button.renderSimple "Square (1:1)" <| fun () -> this.DialogService.Close(square)
                Button.renderSimple "Landscape (3:2)" <| fun () -> this.DialogService.Close(landscape)
            }
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                
                Button.renderSimple "Ok" <| fun () -> this.DialogService.Close(PromptDialogResult.Inspiration2Prompt this.Prompt)
                Button.renderSimple "Cancel" <| fun () -> this.DialogService.Close(null)
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
        
