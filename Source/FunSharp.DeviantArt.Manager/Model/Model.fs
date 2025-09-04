namespace FunSharp.DeviantArt.Manager

open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager
open Microsoft.AspNetCore.Components.Forms

module Model =
    
    type UploadedFile = {
        FileName: string
        PreviewUrl: string
        Content: byte array
        Metadata: DeviationMetadata
    }
    
    [<RequireQualifiedAccess>]
    module UploadedFile =
        
        let empty = {
            FileName = ""
            PreviewUrl = ""
            Content = Array.empty
            Metadata = DeviationMetadata.empty
        }
    
    type State = {
        Page: Page
        Error: string option
        IsBusy: bool
        
        UploadedFiles: UploadedFile array
        
        StashedDeviations: StashedDeviation array
        PublishedDeviations: PublishedDeviation array
    }

    [<RequireQualifiedAccess>]
    module State =
        
        let empty = {
            IsBusy = false
            Page = Page.Home
            Error = None
            
            UploadedFiles = Array.empty
            
            StashedDeviations = Array.empty
            PublishedDeviations = Array.empty
        }
        
    type Message =
        | SetPage of Page
        
        | Error of exn
        | ClearError
        | Done
        
        | LoadDeviations
        | LoadedDeviations of local: UploadedFile array * stashed: StashedDeviation array * published: PublishedDeviation array
        
        | UploadFiles of IBrowserFile[]
        | UploadedFiles of fileName: string * previewUrl: string * content: byte array
        
        | UpdateUploadedFile of UploadedFile
        
        | SaveUploadedFile of UploadedFile
        | SaveStashedFile of StashedDeviation
        
        | DeleteLocalFile of UploadedFile
        
        | Stash of UploadedFile
        | Stashed of file: UploadedFile * deviation: StashedDeviation
