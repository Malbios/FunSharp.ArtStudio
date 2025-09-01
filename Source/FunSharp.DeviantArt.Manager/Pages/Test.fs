namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Elmish
open Microsoft.AspNetCore.Components.Forms
open FunSharp.DeviantArt.Manager.Model
open Radzen.Blazor
open Toolbelt.Blazor.FileDropZone

type Test() =
    inherit ElmishComponent<Test.State, Test.Message>()
    
    override _.CssScope = CssScopes.MyApp
    
    override this.ShouldRender(oldModel, newModel) =
        
        oldModel.Value <> newModel.Value

    override this.View model dispatch =
        comp<RadzenStack> {
            comp<FileDropZone> {
                attr.style "padding: 2rem;"
                
                comp<InputFile> {
                  attr.callback "OnChange" (fun e -> dispatch (Test.Message.SetValue e))
                }
            }
            
            text $"model.Value: {model.Value}"
        }

[<RequireQualifiedAccess>]
module Test =
    
    let update (message: Test.Message) (model: Test.State) =
        
        match message with

        | Test.DoStuff ->
            model, Cmd.none
            
        | Test.SetValue s ->
            { model with Value = s }, Cmd.none
