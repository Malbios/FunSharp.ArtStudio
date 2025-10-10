namespace FunSharp.ArtStudio.Model

type Metadata = {
    Title: string
    Gallery: string
    IsMature: bool
}

[<RequireQualifiedAccess>]
module Metadata =
    
    let defaults = {
        Title = ""
        Gallery = ""
        IsMature = false
    }
