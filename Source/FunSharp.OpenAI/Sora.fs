namespace FunSharp.OpenAI

module Sora =
    
    type ImageType =
        | Landscape
        | Square
        | Portrait
    
    type Client() =
        
        member _.CreateImage(prompt: string, imageType: ImageType) =
            
            ()
