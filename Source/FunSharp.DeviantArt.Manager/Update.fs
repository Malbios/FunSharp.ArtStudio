namespace FunSharp.DeviantArt.Manager

open Elmish
open FunSharp.DeviantArt.Manager.Model
open Microsoft.Extensions.Logging

module Update =
    
    let update (logger: ILogger) message (model: Application.State) =
    
        match message with
        | Application.SetPage page ->
            { model with Page = page }, Cmd.none

        | Application.Error ex ->
            { model with Error = Some ex.Message }, Cmd.none
        | Application.ClearError ->
            { model with Error = None }, Cmd.none

        | Application.Message.TestMessage message ->
            let testModel, cmd = Pages.Test.update message model.TestState
            { model with TestState = testModel }, cmd
