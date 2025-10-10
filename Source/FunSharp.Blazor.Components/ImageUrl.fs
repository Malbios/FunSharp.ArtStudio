namespace FunSharp.Blazor.Components

open System
open System.Web
open Bolero

[<RequireQualifiedAccess>]
module ImageUrl =
    
    let render (imageUrl: Uri option) =
            
        match imageUrl with
        | None ->
            Node.Empty()
            
        | Some imageUrl ->
            
            imageUrl
            |> FunSharp.Common.Uri.lastSegment
            |> HttpUtility.UrlDecode
            |> Some
            |> fun x -> Link.renderSimple x imageUrl
