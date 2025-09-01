namespace FunSharp.DeviantArt.Manager.Model

[<RequireQualifiedAccess>]
module Test =
    
    type State = {
        Value: string
    }

    module State =
        
        let initial = {
            Value = ""
        }
        
    type Message =
        | DoStuff
        | SetValue of string
