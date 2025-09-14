namespace FunSharp.DeviantArt.Manager

open System
open Microsoft.AspNetCore.Components.Forms
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager

module Model =
        
    type Settings = {
        Galleries: Gallery array
    }
    
    type Inspiration2Prompt = {
        Inspiration: Uri
        Text: string
    }
    
    type Prompt2LocalDeviation = {
        Prompt: Guid
        ImageUrl: Uri
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
            
            Settings = NotLoaded
            
            Inspirations = NotLoaded
            Prompts = NotLoaded
            LocalDeviations = NotLoaded
            StashedDeviations = NotLoaded
            PublishedDeviations = NotLoaded
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
        
        | AddInspiration of inspirationUrl: Uri
        | AddedInspiration of Inspiration
        | AddInspirationFailed of error: exn * inspirationUrl: Uri
        
        | RemoveInspiration of Inspiration
        
        | Inspiration2Prompt of Inspiration * promptText: string
        | Inspiration2PromptDone of Inspiration * Prompt
        | Inspiration2PromptFailed of error: exn * Inspiration * promptText: string
        
        | AddPrompt of promptText: string
        | AddedPrompt of Prompt
        | AddPromptFailed of error: exn * promptText: string
        
        | RemovePrompt of Prompt
        | ForgetPrompt of Prompt
        
        | Prompt2LocalDeviation of Prompt * imageFile: IBrowserFile
        | Prompt2LocalDeviationDone of Prompt * local: LocalDeviation
        | Prompt2LocalDeviationFailed of error: exn * Prompt * imageFile: IBrowserFile
        
        | AddLocalDeviation of imageFile: IBrowserFile
        | AddLocalDeviations of imageFiles: IBrowserFile[]
        | AddedLocalDeviation of local: LocalDeviation
        | AddLocalDeviationFailed of error: exn * imageFile: IBrowserFile
        
        | UpdateLocalDeviation of local: LocalDeviation
        | UpdatedLocalDeviation of local: LocalDeviation
        | UpdateLocalDeviationFailed of error: exn * local: LocalDeviation
        
        | RemoveLocalDeviation of local: LocalDeviation
        | DeleteLocalDeviation of local: LocalDeviation
        
        | StashDeviation of local: LocalDeviation
        | StashedDeviation of local: LocalDeviation * stashed: StashedDeviation
        | StashDeviationFailed of error: exn * local: LocalDeviation
        
        | AddedStashedDeviation of stashed: StashedDeviation
        | RemoveStashedDeviation of stashed: StashedDeviation
        
        | PublishStashed of stashed: StashedDeviation
        | PublishedStashed of stashed: StashedDeviation * published: PublishedDeviation
        | PublishStashedFailed of error: exn * stashed: StashedDeviation
        
        | AddedPublishedDeviation of published: PublishedDeviation
