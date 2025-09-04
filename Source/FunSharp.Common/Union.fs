namespace FunSharp.Common

open Microsoft.FSharp.Reflection

[<RequireQualifiedAccess>]
module Union =
   
    let toString<'T> x=
        
        FSharpValue.GetUnionFields(x, typeof<'T>)
        |> fun (case, _) -> case.Name
        
    let asStrings<'T> () =
        FSharpType.GetUnionCases typeof<'T>
        |> Array.map _.Name
