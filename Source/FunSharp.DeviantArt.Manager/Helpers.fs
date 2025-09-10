namespace FunSharp.DeviantArt.Manager

open Bolero
open Bolero.Html
open FunSharp.Common
open Microsoft.AspNetCore.Components.Web
open Microsoft.JSInterop

[<RequireQualifiedAccess>]
module Helpers =
    
    let stashUrl itemId =
        
        $"https://sta.sh/0{Base36.encode itemId}"
