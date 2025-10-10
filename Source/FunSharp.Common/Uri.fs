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
        
    let tryParse (s: string) : Uri option =
        
        match Uri.TryCreate(s, UriKind.Absolute) with
        | true, uri -> Some uri
        | false, _ -> None
