namespace FunSharp.ArtStudio.Client.Pages

open Bolero
open Bolero.Html
open FunSharp.Common
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.ArtStudio.Model
open FunSharp.ArtStudio.Client.Model
open FunSharp.ArtStudio.Client.Components

type ChatGPT() =
    inherit ElmishComponent<State, Message>()
    
    [<Inject>]
    member val DialogService = Unchecked.defaultof<DialogService> with get, set
            
    override _.CssScope = CssScopes.``FunSharp.ArtStudio.Client``
    
    override this.View model dispatch =
    
        let openPromptDialog (result: ChatGPTResult) =
            
            PromptDialog.OpenAsync(this.DialogService, model.Settings, "ChatGPTResult2SoraTask", result.Text)
            |> Task.map (
                function
                | :? PromptDialogResult as dialogResult ->
                    match dialogResult with
                    | PromptDialogResult.Prompt promptText ->
                        Message.Inspiration2Prompt (result.Task.Inspiration, promptText) |> dispatch
                    | PromptDialogResult.SoraTask (promptText, aspectRatio) ->
                        Message.ChatGPTResult2SoraTask (result, promptText, aspectRatio) |> dispatch
                | _ -> ()
            )
            |> Async.AwaitTask
            |> Async.StartAsTask
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "1rem"
            
            Loadable.render model.ChatGPTResults
            <| fun results ->
                results
                |> StatefulItems.sortBy _.Timestamp
                |> Array.map (fun result ->
                    match result with
                    | StatefulItem.IsBusy _ ->
                        LoadingWidget.render ()
                        
                    | StatefulItem.HasError (result, error) ->
                        comp<RadzenStack> {
                            "Orientation" => Orientation.Vertical
                            "Gap" => "0.5rem"
                            
                            div { $"result: {result.Id}" }
                            div { $"timestamp: {result.Timestamp}" }
                            div { $"text: {result.Text}" }
                            div { $"error: {error}" }
                            
                            Inspiration.render (Some result.Task.Inspiration)
                        }
                        
                    | StatefulItem.Default result ->
                        comp<RadzenStack> {
                            "Orientation" => Orientation.Vertical
                            "JustifyContent" => JustifyContent.Center
                            "AlignItems" => AlignItems.Center
                            "Gap" => "0.5rem"
                            
                            Inspiration.render (Some result.Task.Inspiration)
                                
                            comp<RadzenStack> {
                                "Orientation" => Orientation.Horizontal
                                
                                Button.renderSimple "Forget" (fun () -> Message.ForgetChatGPTResult result |> dispatch)
                                Button.renderSimpleAsync "To Sora Task" (fun () -> openPromptDialog result)
                            }
                        }
                )
                |> Helpers.renderArray
                |> Deviations.render
                
            hr { attr.style "width: 100%;" }
            
            Tasks.render model.ChatGPTTasks _.Timestamp Tasks.chatGPTTaskErrorDetails Tasks.chatGPTTaskDetails
        }
        |> Page.render model dispatch
