namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero.Html
open Microsoft.JSInterop
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Api.Model

[<RequireQualifiedAccess>]
module Inspirations = // TODO: turn into Component because of statefulness
    
    let render parent (jsRuntime: IJSRuntime) (addInspiration: Uri -> unit) (inspiration2Prompt: Inspiration -> string -> unit) (forgetInspiration: Inspiration -> unit) (inspirations: Loadable<Inspiration array>) =
        
        let mutable newInspirationUrl = ""
        let mutable prompts: Map<string, string> = Map.empty
        
        let add () =
            newInspirationUrl |> Uri |> addInspiration
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            ClipboardSnippets.render parent jsRuntime
            
            Loadable.render inspirations
            <| fun inspirations ->
                inspirations
                |> Array.map (fun inspiration ->
                    let key = inspiration.Url.ToString()
                    
                    let prompt =
                        prompts
                        |> Map.tryFind key
                        |> Option.defaultValue ""
                        
                    let updatePrompt (newPrompt: string) =
                        prompts <- prompts |> Map.add key (newPrompt.Trim())
                        
                    let inspiration2Prompt =
                        inspiration2Prompt inspiration
                        
                    concat {
                        inspiration.ImageUrl
                        |> ImageUrl.render
                        
                        inspiration.Url
                        |> Link.render None
                        
                        TextAreaInput.render updatePrompt "Enter prompt..." prompt
                        
                        Button.render parent (fun () -> prompts[key].Trim() |> inspiration2Prompt) "To Prompt"
                        Button.render parent (fun () -> forgetInspiration inspiration) "Forget"
                    }
                    |> Deviation.renderWithContent inspiration.ImageUrl
                )
                |> Helpers.renderArray
                |> Deviations.render
            
            comp<RadzenStack> {
                attr.style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
                
                "Orientation" => Orientation.Horizontal
                
                div {
                    attr.style "width: 100%"
                    TextInput.render (fun newValue -> newInspirationUrl <- newValue) (fun newValue -> newInspirationUrl <- newValue; add ()) "Enter inspiration url..." newInspirationUrl
                }
                
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    
                    Button.render parent add "Add"
                }
            }
        }
