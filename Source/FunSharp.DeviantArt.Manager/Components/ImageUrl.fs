namespace FunSharp.DeviantArt.Manager.Components

open System
open System.Web
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module ImageUrl =
    
    let render (imageUrl: Uri option) =
            
        match imageUrl with
        | None -> Node.Empty ()
        | Some imageUrl ->
            comp<RadzenLink> {
                "Path" => $"{imageUrl}"
                "Text" => (imageUrl |> FunSharp.Common.Uri.lastSegment |> HttpUtility.UrlDecode)
                "Target" => "_blank"
            }
