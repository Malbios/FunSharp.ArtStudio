namespace FunSharp.DeviantArt.Manager.Pages

open System.Collections.Generic
open Bolero
open Bolero.Html
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
            
        let inspiration2Prompt inspiration prompt =
            Message.Inspiration2Prompt (inspiration, prompt) |> dispatch
            
        let forgetInspiration inspiration =
            Message.ForgetInspiration inspiration |> dispatch
            
        let openDialog inspiration = task {
            let snippets =
                match model.Settings with
                | Loaded settings -> settings.Snippets
                | _ -> Array.empty
            
            let parameters = Dictionary<string, obj>(dict [ "Snippets", box snippets ])
            
            let! result = this.DialogService.OpenAsync<Inspiration2PromptDialog>("Inspiration2Prompt", parameters)
            
            match result with
            | :? string as promptText -> inspiration2Prompt inspiration promptText
            | _ -> ()
        }

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
                        
                        Button.renderAsync "To Prompt" (fun () -> openDialog inspiration) false
                        Button.render "Forget" (fun () -> forgetInspiration inspiration) false
                    }
                    |> Deviation.renderWithContent inspiration.ImageUrl None
                )
                |> Helpers.renderArray
                |> Deviations.render
        }
        |> Page.render model dispatch this.NavManager
