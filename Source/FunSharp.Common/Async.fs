namespace FunSharp.Common

[<RequireQualifiedAccess>]
module Async =
    
    let returnM x = async {
        return x
    }
    
    let ignore _ =
        returnM ()

    let tee (f: 'T -> unit) (workflow: Async<'T>) : Async<'T> = async {
        let! value = workflow
        f value
        return value
    }
    
    let map f asyncValue = async {
        let! v = asyncValue
        return f v
    }

    let bind f asyncValue = async {
        let! v = asyncValue
        return! f v
    }
    
    let catch<'T> (input: Async<'T>) =
        input
        |> Async.Catch
        |> map (function
            | Choice1Of2 result -> Ok result
            | Choice2Of2 ex -> Error ex
        )
