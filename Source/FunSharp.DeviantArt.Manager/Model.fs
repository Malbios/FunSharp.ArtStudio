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
        
    type Settings = {
        Galleries: Gallery array
    }
    
    type State = {
        Page: Page
        
        Settings: Loadable<Settings>
        
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
            
            Settings = Loadable.NotLoaded
            
            Inspirations = Loadable.NotLoaded
            Prompts = Loadable.NotLoaded
            LocalDeviations = Loadable.NotLoaded
            StashedDeviations = Loadable.NotLoaded
            PublishedDeviations = Loadable.NotLoaded
        }
        
    type Message =
        | SetPage of Page
        
        | Initialize
        
        | LoadSettings
        | LoadedSettings of Loadable<Settings>
        
        | LoadInspirations
        | LoadedInspirations of Loadable<Inspiration array>
        
        | LoadPrompts
        | LoadedPrompts of Loadable<Prompt array>
        
        | LoadLocalDeviations
        | LoadedLocalDeviations of Loadable<LocalDeviation array>
        
        | LoadStashedDeviations
        | LoadedStashedDeviations of Loadable<StashedDeviation array>
        
        | LoadPublishedDeviations
        | LoadedPublishedDeviations of Loadable<PublishedDeviation array>
        
        | AddInspiration of Inspiration
        | AddInspirationFailed of error: exn * inspiration: Inspiration
        
        | Inspiration2Prompt of inspiration: Inspiration * prompt: Prompt
        | Inspiration2PromptFailed of error: exn * inspiration: Inspiration * prompt: Prompt
        
        | Prompt2LocalDeviation of prompt: Prompt * local: LocalDeviation
        | Prompt2LocalDeviationFailed of error: exn * prompt: Prompt * local: LocalDeviation
        
        | StashDeviation of LocalDeviation
        | StashedDeviation of local: LocalDeviation * stashed: StashedDeviation
        | StashDeviationFailed of error: exn * local: LocalDeviation
        
        | PublishStashed of StashedDeviation
        | PublishedDeviation of stashed: StashedDeviation * published: PublishedDeviation
        | PublishStashedFailed of error: exn * stashed: StashedDeviation
        
        | ProcessImages of IBrowserFile[]
        | ProcessedImage of LocalDeviation
        | ProcessImageFailed of error: exn * file: IBrowserFile
        
        | UploadLocalDeviation of LocalDeviation
        | UploadLocalDeviationFailed of error: exn * local: LocalDeviation
        
        | UpdateLocalDeviation of LocalDeviation
        | UpdateLocalDeviationFailed of error: exn * local: LocalDeviation
