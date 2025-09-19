namespace FunSharp.DeviantArt.Manager

open Bolero

[<RequireQualifiedAccess>]
type Page =
    | [<EndPoint "/">] Home
    | [<EndPoint "/inspirations">] Inspirations
    | [<EndPoint "/prompts">] Prompts
    | [<EndPoint "/local-deviations">] LocalDeviations
    | [<EndPoint "/stashed-deviations">] StashedDeviations
    | [<EndPoint "/published-deviations">] PublishedDeviations
    | [<EndPoint "/not-found">] NotFound
