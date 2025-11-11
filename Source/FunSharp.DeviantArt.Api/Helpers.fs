namespace FunSharp.DeviantArt.Api.Model

open FunSharp.Common

module Helpers =
    
    let stashUrl itemId =
        
        $"https://sta.sh/0{Base36.encode itemId}"
    
    let stashEditUrl itemId =
        
        $"https://www.deviantart.com/_deviation_submit/?deviationid={itemId}"
