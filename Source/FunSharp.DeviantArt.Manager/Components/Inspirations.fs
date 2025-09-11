namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero.Html
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Api.Model

[<RequireQualifiedAccess>]
module Inspirations =
    
    let render parent (addInspiration: Uri -> unit) (inspiration2Prompt: Inspiration -> string -> unit) (inspirations: Loadable<Inspiration array>) =
        
        let mutable newInspirationUrl = ""
        let mutable prompts: Map<string, string> = Map.empty
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            comp<RadzenStack> {
                attr.style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
                
                "Orientation" => Orientation.Horizontal
                
                div {
                    attr.style "width: 100%"
                    TextInput.render (fun newValue -> newInspirationUrl <- newValue) "Enter inspiration url..." newInspirationUrl
                }
                
                Button.render parent (fun () -> addInspiration (Uri newInspirationUrl)) "Add"
            }
            
            Loadable.render inspirations
            <| fun inspirations ->
                inspirations
                |> Array.map (fun inspiration ->
                    let key = inspiration.Url.ToString()
                    
                    let prompt =
                        prompts
                        |> Map.tryFind key
                        |> Option.defaultValue ""
                        
                    let updatePrompt newPrompt =
                        prompts <- prompts |> Map.add key newPrompt
                        
                    let inspiration2Prompt =
                        inspiration2Prompt inspiration
                        
                    concat {
                        inspiration.ImageUrl
                        |> ImageUrl.render
                        
                        inspiration.Url
                        |> Link.render None
                        
                        TextInput.render updatePrompt "Enter prompt..." prompt
                        
                        Button.render parent (fun () -> inspiration2Prompt prompts[key]) "To Prompt"
                    }
                    |> Deviation.render inspiration.ImageUrl
                )
                |> Helpers.renderArray
                |> Deviations.render
        }
