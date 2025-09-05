namespace FunSharp.DeviantArt.Manager

open Bolero
open Bolero.Html
open FunSharp.Common

[<RequireQualifiedAccess>]
module Helpers =
    
    let renderArray (nodes: Node array) =
        concat {
            for node in nodes do node
        }
    
    let renderList (nodes: Node list) =
        concat {
            for node in nodes do node
        }
    
    let stashUrl itemId =
        
        $"https://sta.sh/0{Base36.encode itemId}"
