namespace FunSharp.Common

[<RequireQualifiedAccess>]
module String =
    
    let truncate maxLength (value: string) =
        
        if value.Length > maxLength then
            value.Substring(0, maxLength)
         else
             value
