namespace FunSharp.DeviantArt.Manager.Model

open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager
open Microsoft.AspNetCore.Components.Forms

module Application =
    
    type AuthData = {
        ClientId: string
        ClientSecret: string
        AccessToken: string
        RefreshToken: string
    }
    
    [<RequireQualifiedAccess>]
    module AuthData =
        
        let empty = {
            ClientId = ""
            ClientSecret = ""
            AccessToken = ""
            RefreshToken = ""
        }
    
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
        
        AuthData: AuthData
        Client: Client option
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
            
            AuthData = AuthData.empty
            Client = None
        }
        
    type Message =
        | SetPage of Page
        
        | Error of exn
        | ClearError
        
        | UploadImages of IBrowserFile[]
        | FinishUpload of fileName: string * previewUrl: string * content: byte array
        
        | UpdateUploadedFile of UploadedFile
        
        | LoadDeviations
        | LoadedDeviations of stashed: StashedDeviation array * published: PublishedDeviation array
        
        | Stash of UploadedFile
        | Stashed of file: UploadedFile * deviation: StashedDeviation
        
        | SaveDeviation of file: UploadedFile * deviation: DeviationData
        | SavedDeviation
        
        | UpdateAuthData of AuthData
        | SetupClient
