namespace FunSharp.ArtStudio.Client

open Bolero

[<RequireQualifiedAccess>]
type Page =
    | [<EndPoint "/">] Home
    | [<EndPoint "/add-inspiration">] AddInspiration
    | [<EndPoint "/inspirations">] Inspirations
    | [<EndPoint "/prompts">] Prompts
    | [<EndPoint "/sora">] Sora
    | [<EndPoint "/local-deviations">] LocalDeviations
    | [<EndPoint "/stashed-deviations">] StashedDeviations
    | [<EndPoint "/published-deviations">] PublishedDeviations
    | [<EndPoint "/not-found">] NotFound
