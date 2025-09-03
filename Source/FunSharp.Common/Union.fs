namespace FunSharp.Common

open Microsoft.FSharp.Reflection

[<RequireQualifiedAccess>]
module Union =
   
    let toString x=
        
        FSharpValue.GetUnionFields(x, typeof<'a>)
        |> fun (case, _) -> case.Name
        
    let asStrings<'T> () =
        FSharpType.GetUnionCases typeof<'T>
        |> Array.map _.Name
