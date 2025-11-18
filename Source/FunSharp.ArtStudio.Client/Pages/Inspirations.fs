namespace FunSharp.ArtStudio.Client.Pages

open Bolero
open Bolero.Html
open FunSharp.Common
open Microsoft.AspNetCore.Components
open Microsoft.JSInterop
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.ArtStudio.Client.Model
open FunSharp.ArtStudio.Client.Components

type Inspirations() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.Inspirations
    
    [<Inject>]
    member val DialogService = Unchecked.defaultof<DialogService> with get, set
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<IJSRuntime> with get, set
    
    override this.View model dispatch =
            
        let openPromptDialog inspiration =
            Helpers.readClipboard this.JSRuntime
            |> Async.bind (fun text ->
                PromptDialog.OpenAsync(this.DialogService, model.Settings, "Inspiration2Prompt", text)
                |> Task.map (
                    function
                    | :? PromptDialogResult as result ->
                        match result with
                        | PromptDialogResult.Prompt promptText ->
                            Message.Inspiration2Prompt (inspiration, promptText) |> dispatch
                        | PromptDialogResult.SoraTask (promptText, aspectRatio) ->
                            Message.Inspiration2SoraTask (inspiration, promptText, aspectRatio) |> dispatch
                    | _ -> ()
                )
                |> Async.AwaitTask
            )
            |> Async.StartAsTask
            
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            Loadable.render model.Inspirations
            <| fun inspirations ->
                inspirations
                |> StatefulItems.sortBy _.Timestamp
                |> Array.map (fun inspiration ->
                    let inspiration = StatefulItem.valueOf inspiration
                    
                    concat {
                        inspiration.Timestamp.ToString() |> text
                        
                        Button.renderSimple "To ChatGPT" <| fun () ->  Message.Inspiration2ChatGPTTask inspiration |> dispatch
                        Button.renderSimpleAsync "To Prompt" <| fun () -> openPromptDialog inspiration
                        Button.renderSimple "Forget" <| fun () -> Message.ForgetInspiration inspiration |> dispatch
                    }
                    |> Deviation.renderWithContent inspiration.ImageUrl None
                )
                |> Deviations.render
        }
        |> Page.render model dispatch
