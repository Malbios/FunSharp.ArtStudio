namespace FunSharp.Common

[<RequireQualifiedAccess>]
module Async =
    
    let returnM x = async {
        return x
    }
    
    let ignore _ =
        returnM ()
    
    let map f asyncValue = async {
        let! v = asyncValue
        return f v
    }

    let bind f asyncValue = async {
        let! v = asyncValue
        return! f v
    }
    
    let tee f a = async {
        let! result = a
        f result
        return result
    }
    
    let catch<'T> (input: Async<'T>) =
        input
        |> Async.Catch
        |> map (function
            | Choice1Of2 result -> Ok result
            | Choice2Of2 ex -> Error ex
        )

    let getOrFail<'T> (input: Async<'T>) =
        input
        |> catch
        |> bind (fun r ->
            match r with
            | Ok x -> returnM x
            | Error ex -> raise ex
        )
