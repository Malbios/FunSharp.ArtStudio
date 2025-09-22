namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Bolero.Html.attr
open FunSharp.Blazor.Components
open Microsoft.AspNetCore.Components
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.Components
open Radzen
open Radzen.Blazor

type Inspirations() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.Inspirations
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<_> with get, set
    
    [<Inject>]
    member val NavManager: NavigationManager = Unchecked.defaultof<_> with get, set
    
    override this.View model dispatch =
        
        let mutable prompts: Map<string, string> = Map.empty
            
        let inspiration2Prompt inspiration prompt =
            Message.Inspiration2Prompt (inspiration, prompt) |> dispatch
            
        let forgetInspiration inspiration =
            Message.ForgetInspiration inspiration |> dispatch

        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            div {
                style "padding: 0.5rem; border: 2px solid gray; border-radius: 8px;"
                
                let snippets =
                    match model.Settings with
                    | Loaded settings -> Some settings.Snippets
                    | _ -> None

                ClipboardSnippets.render this this.JSRuntime snippets
            }
            
            Loadable.render model.Inspirations
            <| fun inspirations ->
                inspirations
                |> StatefulItemArray.sortBy _.Timestamp
                |> Array.map (fun inspiration ->
                    let inspiration = StatefulItem.valueOf inspiration
                    let key = inspiration.Url.ToString()
                    
                    let prompt =
                        prompts
                        |> Map.tryFind key
                        |> Option.defaultValue ""
                        
                    let updatePrompt (newPrompt: string) =
                        prompts <- prompts |> Map.add key (newPrompt.Trim())
                        
                    concat {
                        inspiration.Timestamp.ToString() |> text
                        
                        TextAreaInput.render updatePrompt "Enter prompt..." prompt
                        
                        Button.render this (fun () -> prompts[key].Trim() |> inspiration2Prompt inspiration) false "To Prompt"
                        Button.render this (fun () -> forgetInspiration inspiration) false "Forget"
                    }
                    |> Deviation.renderWithContent inspiration.ImageUrl None
                )
                |> Helpers.renderArray
                |> Deviations.render
        }
        |> Page.render this model dispatch this.NavManager
