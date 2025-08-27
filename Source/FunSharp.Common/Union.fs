namespace FunSharp.Common

open Microsoft.FSharp.Reflection

[<RequireQualifiedAccess>]
module Union =
   
    let toString (x:'a) =
        
        FSharpValue.GetUnionFields(x, typeof<'a>)
        |> fun (case, _) -> case.Name
