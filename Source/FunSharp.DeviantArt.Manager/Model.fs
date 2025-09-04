namespace FunSharp.DeviantArt.Manager

open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager
open Microsoft.AspNetCore.Components.Forms

module Model =
    
    type Loadable<'T> =
        | NotLoaded
        | Loading
        | Loaded of 'T
        | LoadingFailed of exn
    
    type State = {
        Page: Page
        
        Inspirations: Loadable<Inspiration array>
        Prompts: Loadable<Prompt array>
        LocalDeviations: Loadable<LocalDeviation array>
        StashedDeviations: Loadable<StashedDeviation array>
        PublishedDeviations: Loadable<PublishedDeviation array>
    }

    [<RequireQualifiedAccess>]
    module State =
        
        let empty = {
            Page = Page.Home
            
            Inspirations = Loadable.NotLoaded
            Prompts = Loadable.NotLoaded
            LocalDeviations = Loadable.NotLoaded
            StashedDeviations = Loadable.NotLoaded
            PublishedDeviations = Loadable.NotLoaded
        }
        
    type Message =
        | SetPage of Page
        
        | Initialize
        
        | LoadInspirations
        | LoadedInspirations of Loadable<Inspiration array>
        | LoadPrompts
        | LoadedPrompts of Loadable<Prompt array>
        | LoadLocalDeviation
        | LoadedLocalDeviation of Loadable<LocalDeviation array>
        | LoadStashedDeviation
        | LoadedStashedDeviation of Loadable<StashedDeviation array>
        | LoadPublishedDeviation
        | LoadedPublishedDeviation of Loadable<PublishedDeviation array>
        
        | AddInspiration of Inspiration
        | Inspiration2Prompt of Inspiration * Prompt
        | Prompt2LocalDeviation of Prompt * LocalDeviation
        | StashDeviation of LocalDeviation
        | PublishStashed of StashedDeviation
        
        | UploadLocalDeviations of IBrowserFile[]

    type ImageType =
        | Spicy
        | Scenery
        | RandomPile
        
    type Gallery =
        | Featured
        | Caricatures
        | Spicy
        | Scenery
        | RandomPile
