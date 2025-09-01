namespace FunSharp.DeviantArt.Manager

open Bolero

type Page =
    | [<EndPoint "/">] Home
    | [<EndPoint "/not-found">] NotFound
    | [<EndPoint "/test">] Test
