namespace FunSharp.DeviantArt.Manager.Model

open System
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Model
open FunSharp.DeviantArt.Manager

type Image = {
    Name: string
    ContentType: string
    Content: byte array
}

type Settings = {
    Galleries: Gallery array
    Snippets: ClipboardSnippet array
}

module AddInspiration =
    
    type State = {
        IsBusy: bool
        Url: Uri option
        Error: exn option
    }
    
    [<RequireQualifiedAccess>]
    module State =
        
        let empty = {
            IsBusy = false
            Url = None
            Error = None
        }
        
type Inspiration2Prompt = {
    Inspiration: Uri
    Text: string
}

type Prompt2LocalDeviation = {
    Prompt: Guid
    ImageUrl: Uri
}

type StatefulItem<'T> =
    | Default of 'T
    | IsBusy of 'T
    | HasError of 'T * exn
    
[<RequireQualifiedAccess>]
module StatefulItem =
    
    let valueOf =
        function
        | Default item -> item
        | IsBusy item -> item
        | HasError (item, _) -> item
    
[<RequireQualifiedAccess>]
module StatefulItemArray =

    let sortBy projection items =
        items |> Array.sortBy (StatefulItem.valueOf >> projection)

    let sortByDescending projection items =
        items |> Array.sortByDescending (StatefulItem.valueOf >> projection)
    
[<RequireQualifiedAccess>]
module LoadableStatefulItemArray =
    
    let withNew<'T> (newItem: 'T) (items: Loadable<StatefulItem<'T> array>) =
        
        match items with
        | Loaded items -> [|StatefulItem.Default newItem|] |> Array.append items |> Loaded
        | x -> x
        
    let without<'T> (condition: 'T -> bool) (items: Loadable<StatefulItem<'T> array>) =
        
        match items with
        | Loaded items ->
            items
            |> Array.filter (fun item ->
                match item with
                | Default item
                | IsBusy item
                | HasError (item, _) ->
                    condition item
            )
            |> Loadable.Loaded
            
        | x -> x
        
type State = {
    Page: Page
    Settings: Loadable<Settings>
    
    AddInspirationState: AddInspiration.State
    
    Inspirations: Loadable<StatefulItem<Inspiration> array>
    Prompts: Loadable<StatefulItem<Prompt> array>
    LocalDeviations: Loadable<StatefulItem<LocalDeviation> array>
    StashedDeviations: Loadable<StatefulItem<StashedDeviation> array>
    PublishedDeviations: Loadable<StatefulItem<PublishedDeviation> array>
}

[<RequireQualifiedAccess>]
module State =
    
    let empty = {
        Page = Page.Home
        Settings = NotLoaded
        
        AddInspirationState = AddInspiration.State.empty
        
        Inspirations = NotLoaded
        Prompts = NotLoaded
        LocalDeviations = NotLoaded
        StashedDeviations = NotLoaded
        PublishedDeviations = NotLoaded
    }
    
    let isBusy (identifier: 'T -> bool) (collection: Loadable<StatefulItem<'T> array>) =
        
        match collection with
        | Loaded items ->
            items
            |> Array.map (fun item ->
                match item with
                | Default item ->
                    if identifier item then
                        IsBusy item
                    else
                        Default item
                | IsBusy item -> IsBusy item
                | HasError (item, error) ->
                    if identifier item then
                        IsBusy item
                    else
                        HasError (item, error)
            )
            |> Loadable.Loaded
        | other -> other
    
    let isDefault (identifier: 'T -> bool) (collection: Loadable<StatefulItem<'T> array>) =
        
        match collection with
        | Loaded items ->
            items
            |> Array.map (fun item ->
                match item with
                | Default item -> Default item
                | IsBusy item ->
                    if identifier item then
                        Default item
                    else
                        IsBusy item
                | HasError (item, error) ->
                    if identifier item then
                        Default item
                    else
                        HasError (item, error)
            )
            |> Loadable.Loaded
        | other -> other
    
    let hasError (identifier: 'T -> bool) (error: exn) (collection: Loadable<StatefulItem<'T> array>) =
        
        match collection with
        | Loaded items ->
            items
            |> Array.map (fun item ->
                match item with
                | Default item ->
                    if identifier item then
                        HasError (item, error)
                    else
                        Default item
                | IsBusy item ->
                    if identifier item then
                        HasError (item, error)
                    else
                        IsBusy item
                | HasError (item, oldError) -> 
                    if identifier item then
                        HasError (item, error)
                    else
                        HasError (item, oldError)
            )
            |> Loadable.Loaded
        | other -> other
        
    let without (identifier: 'T -> bool) (collection: Loadable<StatefulItem<'T> array>) =
        
        match collection with
        | Loaded items ->
            items
            |> Array.filter (fun item ->
                match item with
                | Default item -> identifier item |> not
                | IsBusy item -> identifier item |> not
                | HasError (item, _) -> identifier item |> not
            )
            |> Loadable.Loaded
        | other -> other
        
    let withUpdated (identifier: 'T -> bool) (newValue: 'T) (collection: Loadable<StatefulItem<'T> array>) =
        
        match collection with
        | Loaded items ->
            items
            |> Array.map (fun item ->
                match item with
                | Default item ->
                    if identifier item then newValue else item
                    |> Default
                | IsBusy item ->
                    if identifier item then newValue else item
                    |> IsBusy
                | HasError (item, error) ->
                    if identifier item then (newValue, error) else (item, error)
                    |> HasError
            )
            |> Loadable.Loaded
        | other -> other
    
type Message =
    | SetPage of Page
    
    | LoadAll

    | LoadSettings
    | LoadedSettings of Loadable<Settings>
    
    | LoadInspirations
    | LoadedInspirations of Loadable<StatefulItem<Inspiration> array>
    
    | LoadPrompts
    | LoadedPrompts of Loadable<StatefulItem<Prompt> array>
    
    | LoadLocalDeviations
    | LoadedLocalDeviations of Loadable<StatefulItem<LocalDeviation> array>
    
    | LoadStashedDeviations
    | LoadedStashedDeviations of Loadable<StatefulItem<StashedDeviation> array>
    
    | LoadPublishedDeviations
    | LoadedPublishedDeviations of Loadable<StatefulItem<PublishedDeviation> array>
    
    | ChangeNewInspirationUrl of url: string
    
    | AddInspiration
    | AddedInspiration of Inspiration
    | AddInspirationFailed of error: exn
    
    | RemoveInspiration of Inspiration
    | ForgetInspiration of Inspiration
    
    | Inspiration2Prompt of Inspiration * promptText: string
    | Inspiration2PromptDone of Inspiration * Prompt
    | Inspiration2PromptFailed of error: exn * Inspiration * promptText: string
    
    | AddPrompt of promptText: string
    | AddedPrompt of Prompt
    | AddPromptFailed of error: exn * promptText: string
    
    | EditPrompt of Prompt * promptText: string
    | RemovePrompt of Prompt
    | ForgetPrompt of Prompt
    
    | Prompt2LocalDeviation of Prompt * Image
    | Prompt2LocalDeviationDone of Prompt * local: LocalDeviation
    | Prompt2LocalDeviationFailed of error: exn * Prompt * Image
    
    | AddedLocalDeviation of local: LocalDeviation
    
    | UpdateLocalDeviation of local: LocalDeviation
    | UpdatedLocalDeviation of local: LocalDeviation
    | UpdateLocalDeviationFailed of error: exn * local: LocalDeviation
    
    | RemoveLocalDeviation of local: LocalDeviation
    | ForgetLocalDeviation of local: LocalDeviation
    
    | StashDeviation of local: LocalDeviation
    | StashedDeviation of local: LocalDeviation * stashed: StashedDeviation
    | StashDeviationFailed of error: exn * local: LocalDeviation
    
    | AddedStashedDeviation of stashed: StashedDeviation
    | RemoveStashedDeviation of stashed: StashedDeviation
    
    | PublishStashed of stashed: StashedDeviation
    | PublishedStashed of stashed: StashedDeviation * published: PublishedDeviation
    | PublishStashedFailed of error: exn * stashed: StashedDeviation
    
    | AddedPublishedDeviation of published: PublishedDeviation
