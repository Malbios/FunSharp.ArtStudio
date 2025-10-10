namespace FunSharp.Common

[<RequireQualifiedAccess>]
module String =
    
    let truncate maxLength (value: string) =
        
        if value.Length > maxLength then
            value.Substring(0, maxLength)
         else
             value
             
    let trim (value: string) =
        value.Split([|"\r\n"; "\n"|], System.StringSplitOptions.TrimEntries)
        |> String.concat "\n"
