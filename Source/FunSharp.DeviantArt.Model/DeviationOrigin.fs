namespace FunSharp.DeviantArt.Model

[<RequireQualifiedAccess>]
type DeviationOrigin =
    | None
    | Prompt of Prompt
    | Inspiration of Inspiration
