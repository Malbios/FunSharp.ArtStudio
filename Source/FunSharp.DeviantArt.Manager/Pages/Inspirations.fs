namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open FunSharp.Common
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.Components

type Inspirations() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.Inspirations
    
    [<Inject>]
    member val NavManager: NavigationManager = Unchecked.defaultof<_> with get, set
    
    [<Inject>]
    member val DialogService = Unchecked.defaultof<DialogService> with get, set
    
    override this.View model dispatch =
            
        let forgetInspiration inspiration =
            Message.ForgetInspiration inspiration |> dispatch
            
        let openPromptDialog inspiration =
            PromptDialog.OpenAsync(this.DialogService, model.Settings, "Inspiration2Prompt")
            |> Task.map (
                function
                | :? string as promptText -> Message.Inspiration2Prompt (inspiration, promptText) |> dispatch
                | _ -> ()
            )

        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            Loadable.render model.Inspirations
            <| fun inspirations ->
                inspirations
                |> StatefulItemArray.sortBy _.Timestamp
                |> Array.map (fun inspiration ->
                    let inspiration = StatefulItem.valueOf inspiration
                    
                    concat {
                        inspiration.Timestamp.ToString() |> text
                        
                        Button.renderAsync "To Prompt" (fun () -> openPromptDialog inspiration) false
                        Button.render "Forget" (fun () -> forgetInspiration inspiration) false
                    }
                    |> Deviation.renderWithContent inspiration.ImageUrl None
                )
                |> Helpers.renderArray
                |> Deviations.render
        }
        |> Page.render model dispatch this.NavManager
