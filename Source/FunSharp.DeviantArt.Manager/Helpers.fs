namespace FunSharp.DeviantArt.Manager

open FunSharp.Common

[<RequireQualifiedAccess>]
module Helpers =
    
    let stashUrl itemId =
        
        $"https://sta.sh/0{Base36.encode itemId}"
