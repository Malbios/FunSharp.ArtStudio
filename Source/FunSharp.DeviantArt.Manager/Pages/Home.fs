namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Bolero.Html.attr
open Microsoft.AspNetCore.Components
open Microsoft.JSInterop
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.Components

type Home() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.Home
    
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<IJSRuntime> with get, set
    
    [<Inject>]
    member val NavManager: NavigationManager = Unchecked.defaultof<_> with get, set
    
    override this.View model dispatch =
        
        printfn "rendering Home view"
            
        let tab label renderAction : Tabs.Item = {
            Label = label
            RenderAction = renderAction
        }
            
        let addInspiration url =
            Message.AddInspiration url |> dispatch
            
        let inspiration2Prompt inspiration prompt =
            Message.Inspiration2Prompt (inspiration, prompt) |> dispatch
            
        let addPrompt text =
            Message.AddPrompt text |> dispatch
            
        let prompt2Deviation prompt file =
            Message.Prompt2LocalDeviation (prompt, file) |> dispatch
            
        let forgetInspiration inspiration =
            Message.ForgetInspiration inspiration |> dispatch
            
        let forgetPrompt prompt =
            Message.ForgetPrompt prompt |> dispatch
            
        let galleries =
            match model.Settings with
            | Loaded settings -> settings.Galleries |> Array.map _.name
            | _ -> Array.empty
        
        let inspirationsCount =
            match model.Inspirations with
            | Loaded inspirations -> inspirations.Length
            | _ -> -1
        
        let promptsCount =
            match model.Prompts with
            | Loaded prompts -> prompts.Length
            | _ -> -1
        
        let localDeviationsCount =
            match model.LocalDeviations with
            | Loaded deviations -> deviations.Length
            | _ -> -1
            
        let stashedDeviationsCount =
            match model.StashedDeviations with
            | Loaded deviations -> deviations.Length
            | _ -> -1
            
        let publishedDeviationsCount =
            match model.PublishedDeviations with
            | Loaded deviations -> deviations.Length
            | _ -> -1
        
        div {
            attr.``class`` "center-wrapper"
            
            div {
                style "height: 100%"
                
                [|
                    tab $"Inspirations ({inspirationsCount})" (fun () ->
                        Inspirations.render this this.JSRuntime addInspiration inspiration2Prompt forgetInspiration model.Inspirations
                    )
                    
                    tab $"Prompts ({promptsCount})" (fun () ->
                        Prompts.render this this.JSRuntime addPrompt prompt2Deviation forgetPrompt model.Prompts
                    )
                    
                    tab $"Local Deviations ({localDeviationsCount})" (fun () ->
                        match localDeviationsCount with
                        | 0 -> text "No items."
                        | _ ->
                            comp<LocalDeviations> {
                                "Galleries" => galleries
                                "Items" => model.LocalDeviations
                                "OnSave" => (fun deviation -> dispatch (Message.UpdateLocalDeviation deviation))
                                "OnStash" => (fun deviation -> dispatch (Message.StashDeviation deviation))
                                "OnForget" => (fun deviation -> dispatch (Message.ForgetLocalDeviation deviation))
                            }
                    )
                    
                    tab $"Stashed Deviations ({stashedDeviationsCount})" (fun () ->
                        match stashedDeviationsCount with
                        | 0 -> text "No items."
                        | _ ->
                            model.StashedDeviations
                            |> StashedDeviations.render this this.JSRuntime
                                (fun deviation -> dispatch (Message.PublishStashed deviation))
                    )
                    
                    tab $"Published Deviations ({publishedDeviationsCount})" (fun () ->
                        match publishedDeviationsCount with
                        | 0 -> text "No items."
                        | _ ->
                            model.PublishedDeviations
                            |> PublishedDeviations.render
                    )
                |]
                |> Tabs.render
            }
        }
