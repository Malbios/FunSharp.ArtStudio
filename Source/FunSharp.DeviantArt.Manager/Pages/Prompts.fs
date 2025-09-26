namespace FunSharp.DeviantArt.Manager.Pages

open System
open System.IO
open System.Threading.Tasks
open Bolero
open Bolero.Html
open FunSharp.DeviantArt.Model
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Forms
open Radzen
open Radzen.Blazor
open FunSharp.Common
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.Components

type Prompts() =
    inherit ElmishComponent<State, Message>()
        
    let maxSize = 1024L * 1024L * 100L // 100 MB
    
    let mutable busy: Map<Guid, bool> = Map.empty
    
    override _.CssScope = CssScopes.Prompts
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<_> with get, set
    
    [<Inject>]
    member val NavManager: NavigationManager = Unchecked.defaultof<_> with get, set
    
    [<Inject>]
    member val DialogService = Unchecked.defaultof<DialogService> with get, set
    
    override this.View model dispatch =
            
        let forgetPrompt prompt =
            
            Message.ForgetPrompt prompt |> dispatch
    
        let processUploadedFile (prompt: Prompt) (file: IBrowserFile) = task {
            busy <- busy |> Map.add prompt.Id true
            
            use stream = file.OpenReadStream(maxAllowedSize = maxSize)
            use ms = new MemoryStream()
            
            do! stream.CopyToAsync(ms)
            
            let image = {
                Name = file.Name
                ContentType = file.ContentType
                Content = ms.ToArray()
            }
            
            Message.Prompt2LocalDeviation (prompt, image) |> dispatch
            
            busy <- busy |> Map.remove prompt.Id
        }
        
        let openNewPromptDialog () =
            
            PromptDialog.OpenAsync(this.DialogService, model.Settings, "New Prompt")
            |> Task.map (
                function
                | :? string as promptText -> Message.AddPrompt promptText |> dispatch
                | _ -> ()
            )
            
        let openEditPromptDialog (prompt: Prompt) =
            
            PromptDialog.OpenAsync(this.DialogService, model.Settings, "Edit Prompt", prompt.Text)
            |> Task.map (
                function
                | :? string as promptText -> Message.UpdatePrompt { prompt with Text = promptText } |> dispatch
                | _ -> ()
            )
        
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
                        LoadingWidget.render ()
                        
                    | HasError (prompt, error) ->
                        concat {
                            text $"prompt: {prompt.Id}"
                            text $"error: {error}"
                        }
                        
                    | Default prompt ->
                        let isBusy = busy |> Map.tryFind prompt.Id |> Option.defaultValue false
                        
                        let processUpload : InputFileChangeEventArgs -> Task =
                            fun (args: InputFileChangeEventArgs) -> processUploadedFile prompt args.File
                        
                        concat {
                            comp<RadzenStack> {
                                "Orientation" => Orientation.Horizontal
                                
                                prompt.Inspiration
                                |> Option.map _.Timestamp.ToString()
                                |> Option.defaultValue "?"
                                |> text
                            
                                match prompt.Inspiration with
                                | Some inspiration ->
                                    Link.render (Some "DA Link") inspiration.Url
                                | None -> ()
                            }
                            
                            comp<RadzenStack> {
                                "Orientation" => Orientation.Horizontal
                                
                                Button.render "Copy Prompt" (fun () -> Helpers.copyToClipboard this.JSRuntime prompt.Text) isBusy
                                Button.renderAsync "Edit Prompt" (fun () -> openEditPromptDialog prompt) isBusy
                                Button.render "Forget" (fun () -> forgetPrompt prompt) isBusy
                            }
                            
                            FileInput.renderAsync false processUpload isBusy
                        }
                        |> Deviation.renderWithContent (prompt.Inspiration |> Option.bind _.ImageUrl) None
                )
                |> Helpers.renderArray
                |> Deviations.render
                
            div {
                attr.style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
                Button.renderAsync "Add new prompt" (fun () -> openNewPromptDialog ()) false
            }
        }
        |> Page.render model dispatch this.NavManager
