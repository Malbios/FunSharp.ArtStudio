namespace FunSharp.DeviantArt.Model

[<RequireQualifiedAccess>]
type DeviationOrigin =
    | None
    | Prompt of Prompt
    | Inspiration of Inspiration

[<RequireQualifiedAccess>]
module DeviationOrigin =
    
    let inspiration origin =
        
        match origin with
        | DeviationOrigin.None -> None
        | DeviationOrigin.Inspiration inspiration -> Some inspiration
        | DeviationOrigin.Prompt prompt -> prompt.Inspiration

    let prompt origin =
        
        match origin with
        | DeviationOrigin.None
        | DeviationOrigin.Inspiration _ -> None
        | DeviationOrigin.Prompt prompt -> Some prompt
