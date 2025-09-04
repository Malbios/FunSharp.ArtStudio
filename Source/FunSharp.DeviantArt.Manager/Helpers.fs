namespace FunSharp.DeviantArt.Manager

open FunSharp.Common
open FunSharp.DeviantArt.Manager.Model

[<RequireQualifiedAccess>]
module Helpers =

    let galleries =
        [ Gallery.Featured, "487A4797-E595-CA89-7083-32FCD1F33831"
          Gallery.Caricatures, "01EFCC0B-6625-48F5-1C09-74B69FFCA526"
          Gallery.Spicy, "EAC6F867-87CA-333C-9C09-74C7587BAFAF"
          Gallery.Scenery, "B6120853-CD73-52D0-35D3-61BC719AE611"
          Gallery.RandomPile, "A5FA99E2-3756-B8A3-E145-59666660C224" ]
        |> Map.ofList

    let gallery (galleryName: string) =
        match galleryName with
        | v when v = "RandomPile" -> Gallery.RandomPile
        | v when v = "Spicy" -> Gallery.Spicy
        | v when v = "Scenery" -> Gallery.Scenery
        | v -> failwith $"unexpected gallery: {v}"

    let galleryId (galleryName: string) = galleries[gallery galleryName]

    let stashUrl itemId =
        $"https://sta.sh/0{Base36.encode itemId}"
