namespace FunSharp.Common

open System

[<RequireQualifiedAccess>]
module Int =
    
    let tryParse (value: string) =
        
        match Int32.TryParse(value) with
        | true, v -> Some v
        | false, _ -> None
