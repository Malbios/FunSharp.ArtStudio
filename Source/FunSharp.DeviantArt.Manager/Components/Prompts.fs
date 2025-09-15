namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero.Html
open Microsoft.AspNetCore.Components.Forms
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Api.Model

[<RequireQualifiedAccess>]
module Prompts = // TODO: turn into Component because of statefulness
    
    let render parent jsRuntime addPrompt prompt2Deviation forgetPrompt prompts =
        
        let mutable newPromptText = ""
        let mutable files: Map<Guid, IBrowserFile> = Map.empty
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            Loadable.render prompts
            <| fun prompts ->
                prompts
                |> Array.map (fun prompt ->
                    let uploadImageFile (args: InputFileChangeEventArgs) =
                        files <- files |> Map.add prompt.Id args.File
                    
                    let prompt2Deviation =
                        prompt2Deviation prompt
                    
                    concat {
                        prompt.Inspiration
                        |> Option.bind _.ImageUrl
                        |> ImageUrl.render
                        
                        match prompt.Inspiration with
                        | None -> ()
                        | Some inspiration ->
                            inspiration.Url |> Link.render None
                        
                        Button.render parent (Helpers.copyToClipboard jsRuntime prompt.Text) "Copy Prompt"
                        
                        FileInput.render false uploadImageFile
                        
                        Button.render parent (fun () -> prompt2Deviation files[prompt.Id]) "To Deviation"
                        Button.render parent (fun () -> forgetPrompt prompt) "Forget"
                    }
                    |> Deviation.renderWithContent (prompt.Inspiration |> Option.bind _.ImageUrl)
                )
                |> Helpers.renderArray
                |> Deviations.render
            
            comp<RadzenStack> {
                attr.style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
                
                "Orientation" => Orientation.Horizontal
                
                div {
                    attr.style "width: 100%;"
                    TextAreaInput.render (fun newValue -> newPromptText <- newValue) "Enter prompt text..." newPromptText
                }
                
                Button.render parent (fun () -> addPrompt newPromptText) "Add"
            }
        }
