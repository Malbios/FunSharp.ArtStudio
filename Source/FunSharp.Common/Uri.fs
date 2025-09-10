namespace FunSharp.Common

open System

[<RequireQualifiedAccess>]
module Uri =

    let segments (uri: Uri) =
        
        uri.Segments
        |> Array.map _.Trim('/')
        |> Array.filter (fun s -> s <> "")
    
    let lastSegment (uri: Uri) =
        
        uri
        |> segments
        |> Array.last
