namespace FunSharp.DeviantArt.Manager.Model

open FunSharp.DeviantArt.Manager

[<RequireQualifiedAccess>]
module Application =
    
    type State = {
        Page: Page
        Error: string option
        TestState: Test.State
    }

    module State =
        
        let initial = {
            Page = Page.Home
            Error = None
            TestState = Test.State.initial
        }
        
    type Message =
        | SetPage of Page
        | Error of exn
        | ClearError
        | TestMessage of Test.Message
