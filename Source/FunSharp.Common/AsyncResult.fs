namespace FunSharp.Common

[<RequireQualifiedAccess>]
module AsyncResult =
    
    let map f =
        Async.map (Result.map f)
        
    let bind f =
        Async.bind (fun x ->
            match x with
            | Ok x -> f x
            | Error e -> Error e |> Async.returnM
        )
        
    let returnM x =
        Ok x |> Async.returnM
        
    let ofResult r = r |> Async.returnM
        
    let ignore ar =
        map ignore ar
        
    let tee f ar =
        ar |> map (fun x -> f x; x)
        
    let sequential actions =
        let folder acc action =
            acc |> bind (fun results -> map (fun result -> result :: results) action)
            
        List.fold folder (returnM []) (List.rev actions)
        
    let getOrFail (ar: Async<Result<'T, exn>>) =
        ar
        |> Async.bind (fun r ->
            match r with
            | Ok x -> Async.returnM x
            | Error ex -> failwith ex.Message
        )
        
module AsyncResultCE =

    type AsyncResultBuilder() =
        member _.Bind(x, f) = AsyncResult.bind f x
        member _.Return(x) = AsyncResult.returnM x
        member _.ReturnFrom(x) = x
        member _.Zero() = AsyncResult.returnM ()

    let asyncResult = AsyncResultBuilder()
