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

type AddInspiration() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.Inspirations
    
    [<Inject>]
    member val NavManager: NavigationManager = Unchecked.defaultof<_> with get, set
    
    override this.View model dispatch =
        
        let addInspiration () =
            
            dispatch Message.AddInspiration
            
        let onChange_NewInspirationUrl (newValue: string) =
            
            if not <| String.IsNullOrWhiteSpace(newValue.Trim()) then
                Message.ChangeNewInspirationUrl newValue |> dispatch
                
        let currentValue = model.AddInspirationState.Url |> Option.map _.ToString() |> Option.defaultValue ""
        let isBusy = model.AddInspirationState.IsBusy
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            comp<RadzenStack> {
                style "padding: 1rem; border: 2px solid gray; border-radius: 8px;"
                
                "Orientation" => Orientation.Horizontal
                
                TextInput.render onChange_NewInspirationUrl (fun _ -> ()) isBusy "Enter inspiration url..." currentValue
                
                Button.render "Add" addInspiration isBusy
                
                div {
                    match model.AddInspirationState.Error with
                    | None ->
                        style "color: red; visibility: hidden;"
                        text "placeholder"
                        
                    | Some error ->
                        style "color: red; visibility: visible;"
                        text error.Message
                }
            }
            
            Loadable.render model.Inspirations
            <| fun inspirations ->
                inspirations
                |> StatefulItems.sortByDescending _.Timestamp
                |> Array.map (fun inspiration ->
                    inspiration
                    |> StatefulItem.valueOf
                    |> _.ImageUrl
                    |> Deviation.renderWithoutContent
                )
                |> Helpers.renderArray
                |> Deviations.render
        }
        |> Page.render model dispatch this.NavManager
