namespace FunSharp.DeviantArt.Manager.Model

module Common =
    
    type ImageType =
        | Spicy
        | Scenery
        | RandomPile
    
    [<Literal>]
    let dbKeyStashedDeviations = "StashedDeviations"
    
    [<Literal>]
    let dbKeyPublishedDeviations = "PublishedDeviations"

    [<Literal>]
    let dbName = "FunSharp.DeviantArt.Manager"
