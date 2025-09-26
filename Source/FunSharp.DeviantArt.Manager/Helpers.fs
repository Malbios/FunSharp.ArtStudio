namespace FunSharp.DeviantArt.Manager

open FunSharp.Common
open Microsoft.JSInterop

[<RequireQualifiedAccess>]
module Helpers =
    
    let stashUrl itemId =
        
        $"https://sta.sh/0{Base36.encode itemId}"
        
    let stashEditUrl itemId =
        
        $"https://www.deviantart.com/_deviation_submit/?deviationid={itemId}"
