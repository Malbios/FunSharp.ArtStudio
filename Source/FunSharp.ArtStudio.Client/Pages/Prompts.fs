namespace FunSharp.ArtStudio.Client.Pages

open System
open System.IO
open System.Threading.Tasks
open Bolero
open Bolero.Html
open FunSharp.ArtStudio.Model
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Forms
open Radzen
open Radzen.Blazor
open FunSharp.Common
open FunSharp.Blazor.Components
open FunSharp.ArtStudio.Client.Model
open FunSharp.ArtStudio.Client.Components

type Prompts() =
    inherit ElmishComponent<State, Message>()
        
    let maxSize = 1024L * 1024L * 100L // 100 MB
    
    let mutable busy: Map<Guid, bool> = Map.empty
    
    override _.CssScope = CssScopes.Prompts
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<_> with get, set
    
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
                |> StatefulItems.sortBy _.Timestamp
                |> Array.map (fun prompt ->
                    match prompt with
                    | StatefulItem.IsBusy _ ->
                        LoadingWidget.render ()
                        
                    | StatefulItem.HasError (prompt, error) ->
                        concat {
                            text $"prompt: {prompt.Id}"
                            text $"error: {error}"
                        }
                        
                    | StatefulItem.Default prompt ->
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
                                    Link.renderSimple (Some "DA Link") inspiration.Url
                                | None -> ()
                            }
                            
                            comp<RadzenStack> {
                                "Orientation" => Orientation.Horizontal
                                
                                Button.render <| {
                                    ButtonProps.defaults with
                                        Text = "Copy Prompt"
                                        Action = ClickAction.Sync <| fun () -> Helpers.copyToClipboard this.JSRuntime prompt.Text
                                        IsBusy = isBusy
                                }
                                
                                Button.render <| {
                                    ButtonProps.defaults with
                                        Text = "Edit Prompt"
                                        Action = ClickAction.Async <| fun () -> openEditPromptDialog prompt
                                        IsBusy = isBusy
                                }
                                
                                Button.render <| {
                                    ButtonProps.defaults with
                                        Text = "Forget"
                                        Action = ClickAction.Sync <| fun () -> forgetPrompt prompt
                                        IsBusy = isBusy
                                }
                            }
                            
                            FileInput.renderAsync false processUpload isBusy
                        }
                        |> Deviation.renderWithContent (prompt.Inspiration |> Option.bind _.ImageUrl) None
                )
                |> Helpers.renderArray
                |> Deviations.render
        }
        |> Page.render model dispatch
