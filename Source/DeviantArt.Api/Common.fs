namespace DeviantArt.Api

[<RequireQualifiedAccess>]
module Async =

    let tee (f: 'T -> unit) (workflow: Async<'T>) : Async<'T> = async {
        let! value = workflow
        f value
        return value
    }
    
    let returnM x = async { return x }
