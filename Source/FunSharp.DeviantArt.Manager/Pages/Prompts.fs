namespace FunSharp.DeviantArt.Manager.Pages

open System
open Bolero
open Bolero.Html
open FunSharp.Blazor.Components
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Forms
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.Components
open Radzen
open Radzen.Blazor

type Prompts() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.Prompts
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<_> with get, set
    
    [<Inject>]
    member val NavManager: NavigationManager = Unchecked.defaultof<_> with get, set
    
    override this.View model dispatch =

        let mutable newPromptText = ""
        let mutable files: Map<Guid, IBrowserFile> = Map.empty
            
        let addPrompt text =
            Message.AddPrompt text |> dispatch
            
        let prompt2Deviation prompt file =
            Message.Prompt2LocalDeviation (prompt, file) |> dispatch
            
        let forgetPrompt prompt =
            Message.ForgetPrompt prompt |> dispatch
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            Loadable.render model.Prompts
            <| fun prompts ->
                prompts
                |> StatefulItemArray.sortBy _.Timestamp
                |> Array.map (fun prompt ->
                    match prompt with
                    | IsBusy _ ->
                        LoadingWidget.render () // TODO: make only the "To Deviation" button busy (Radzen has something there)
                    | HasError (prompt, error) ->
                        concat {
                            text $"prompt: {prompt.Id}"
                            text $"error: {error}"
                        }
                    | Default prompt ->
                        concat {
                            prompt.Inspiration
                            |> Option.map _.Timestamp.ToString()
                            |> Option.defaultValue "?"
                            |> text
                            
                            Button.render this (Helpers.copyToClipboard this.JSRuntime prompt.Text) false "Copy Prompt"
                            
                            FileInput.render false
                                (fun (args: InputFileChangeEventArgs) -> files <- files |> Map.add prompt.Id args.File)
                            
                            Button.render this (fun () -> prompt2Deviation prompt files[prompt.Id]) false "To Deviation"
                            Button.render this (fun () -> forgetPrompt prompt) false "Forget"
                        }
                        |> Deviation.renderWithContent (prompt.Inspiration |> Option.bind _.ImageUrl) None
                )
                |> Helpers.renderArray
                |> Deviations.render
                
            comp<RadzenStack> {
                attr.style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
                
                "Orientation" => Orientation.Horizontal
                
                div {
                    attr.style "width: 100%;"
                    TextAreaInput.render 6 100 (fun newValue -> newPromptText <- newValue) "Enter prompt text..." newPromptText
                }
                
                Button.render this (fun () -> addPrompt newPromptText) false "Add"
            }
        }
        |> Page.render this model dispatch this.NavManager
