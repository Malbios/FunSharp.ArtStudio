namespace FunSharp.DeviantArt.Manager

module Common =
    
    type ImageType =
        | Spicy
        | Scenery
        | RandomPile
    
    [<Literal>]
    let dbKey_Settings = "Settings"
    
    [<Literal>]
    let dbKey_Inspirations = "Inspirations"
    
    [<Literal>]
    let dbKey_LocalDeviations = "LocalDeviations"
    
    [<Literal>]
    let dbKey_StashedDeviations = "StashedDeviations"
    
    [<Literal>]
    let dbKey_PublishedDeviations = "PublishedDeviations"

    [<Literal>]
    let dbName = "FunSharp.DeviantArt.Manager"
