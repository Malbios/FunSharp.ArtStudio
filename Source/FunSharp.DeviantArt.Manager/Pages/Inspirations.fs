namespace FunSharp.DeviantArt.Manager.Pages

open System
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
                    
        let mutable newInspirationUrl = ""
        let mutable prompts: Map<string, string> = Map.empty
            
        let addInspiration () =
            newInspirationUrl |> Uri |> Message.AddInspiration |> dispatch
            
        let inspiration2Prompt inspiration prompt =
            Message.Inspiration2Prompt (inspiration, prompt) |> dispatch
            
        let forgetInspiration inspiration =
            Message.ForgetInspiration inspiration |> dispatch
            
        let onChange_NewInspirationUrl newValue =
            newInspirationUrl <- newValue
            
        let onEnter_NewInspirationUrl newValue =
            onChange_NewInspirationUrl newValue
            addInspiration ()

        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            div {
                style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
                ClipboardSnippets.render this this.JSRuntime
            }
            
            Loadable.render model.Inspirations
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
                        
                        Button.render this (fun () -> prompts[key].Trim() |> inspiration2Prompt) false "To Prompt"
                        Button.render this (fun () -> forgetInspiration inspiration) false "Forget"
                    }
                    |> Deviation.renderWithContent inspiration.ImageUrl
                )
                |> Helpers.renderArray
                |> Deviations.render
            
            comp<RadzenStack> {
                style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
                
                "Orientation" => Orientation.Horizontal
                
                div {
                    style "width: 100%"
                    TextInput.render onChange_NewInspirationUrl onEnter_NewInspirationUrl "Enter inspiration url..." newInspirationUrl
                }
                
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    
                    Button.render this addInspiration false "Add"
                }
            }
        }
        |> Page.render this this.NavManager
