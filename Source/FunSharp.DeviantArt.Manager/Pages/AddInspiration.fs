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
                    
        let mutable newInspirationUrl = ""
            
        let addInspiration () =
            newInspirationUrl |> Uri |> Message.AddInspiration |> dispatch
            
        let onChange_NewInspirationUrl newValue =
            newInspirationUrl <- newValue
            
        let onEnter_NewInspirationUrl newValue =
            onChange_NewInspirationUrl newValue
            addInspiration ()

        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "Gap" => "2rem"
            
            comp<RadzenStack> {
                style "padding: 1rem; border: 2px solid gray; border-radius: 8px;"
                
                "Orientation" => Orientation.Horizontal
                
                div {
                    TextInput.render onChange_NewInspirationUrl onEnter_NewInspirationUrl "Enter inspiration url..." newInspirationUrl
                }
                
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    
                    Button.render this addInspiration false "Add"
                }
            }
            
            Loadable.render model.Inspirations
            <| fun inspirations ->
                inspirations
                |> StatefulItemArray.sortByDescending _.Timestamp
                |> Array.map (fun inspiration ->
                    inspiration
                    |> StatefulItem.valueOf
                    |> _.ImageUrl
                    |> Deviation.renderWithoutContent
                )
                |> Helpers.renderArray
                |> Deviations.render
        }
        |> Page.render this model dispatch this.NavManager
