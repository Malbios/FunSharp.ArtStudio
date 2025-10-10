namespace FunSharp.ArtStudio.Client

open FunSharp.Common

[<RequireQualifiedAccess>]
module Helpers =
        
    let localDeviationsPageSize = 10
    
    let stashUrl itemId =
        
        $"https://sta.sh/0{Base36.encode itemId}"
        
    let stashEditUrl itemId =
        
        $"https://www.deviantart.com/_deviation_submit/?deviationid={itemId}"
