namespace FunSharp.ArtStudio.Model

type Page<'T> = {
    items: 'T array
    offset: int
    total: int
    has_more: bool
}

[<RequireQualifiedAccess>]
module Page =
    
    let empty = {
        items = Array.empty<'T>
        offset = 0
        total = 0
        has_more = false
    }
