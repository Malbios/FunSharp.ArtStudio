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

[<RequireQualifiedAccess>]
type StatefulItem<'T> =
    | Default of 'T
    | IsBusy of 'T
    | HasError of 'T * exn
    
[<RequireQualifiedAccess>]
module StatefulItem =
    
    let valueOf (item: StatefulItem<'T>) =
        
        match item with
        | StatefulItem.Default item
        | StatefulItem.IsBusy item
        | StatefulItem.HasError (item, _) ->
            item
            
    let update (updatedItem: 'T) (item: StatefulItem<'T>) =
        
        match item with
        | StatefulItem.Default _ ->
            StatefulItem.Default updatedItem
        | StatefulItem.IsBusy _ ->
            StatefulItem.IsBusy updatedItem
        | StatefulItem.HasError(_, e) ->
            StatefulItem.HasError(updatedItem, e)
            
    let matches (identifier: 'T -> bool) (item: StatefulItem<'T>) =
        
        item
        |> valueOf
        |> identifier
        
[<RequireQualifiedAccess>]
module StatefulItems =

    let sortBy projection items =
        items |> Array.sortBy (StatefulItem.valueOf >> projection)

    let sortByDescending projection items =
        items |> Array.sortByDescending (StatefulItem.valueOf >> projection)
        
    let withDefaultState<'T> (items: 'T array) =
        items |> Array.map StatefulItem.Default
        
    let without (identifier: 'T -> bool) (items: StatefulItem<'T> array) =
        
        items
        |> Array.filter (fun item ->
            match item with
            | StatefulItem.Default item -> identifier item |> not
            | StatefulItem.IsBusy item -> identifier item |> not
            | StatefulItem.HasError (item, _) -> identifier item |> not
        )
        
    let update (identifier: 'T -> bool) (updatedItem: 'T) (items: StatefulItem<'T> array) =
        
        items
        |> Array.map (fun item ->
            if StatefulItem.matches identifier item then
                StatefulItem.update updatedItem item
            else
                item
        )
    
    let private replaceState (identifier: 'T -> bool) (collection: StatefulItem<'T> array) (newState: 'T -> StatefulItem<'T>) =
        
        collection
        |> Array.map (fun item ->
            let unwrappedItem = StatefulItem.valueOf item

            if identifier unwrappedItem then
                newState unwrappedItem
            else
                item
        )
        
    let setDefault (identifier: 'T -> bool) (items: StatefulItem<'T> array) =
        
        StatefulItem.Default
        |> replaceState identifier items
        
    let setBusy (identifier: 'T -> bool) (items: StatefulItem<'T> array) =
        
        StatefulItem.IsBusy
        |> replaceState identifier items
        
    let setError (identifier: 'T -> bool) (error: exn) (items: StatefulItem<'T> array) =
        
        fun x -> StatefulItem.HasError (x, error)
        |> replaceState identifier items

[<RequireQualifiedAccess>]
module LoadableStatefulItems =
    
    let private modify (change: StatefulItem<'T> array -> StatefulItem<'T> array)
        (items: Loadable<StatefulItem<'T> array>) =
        
        match items with
        | Loadable.Loaded items ->
            change items
            |> Loadable.Loaded
        
        | other -> other
    
    let withNew (newItem: 'T) (items: Loadable<StatefulItem<'T> array>) =
        
        items
        |> modify (fun items -> [| StatefulItem.Default newItem |] |> Array.append items)
        
    let without (identifier: 'T -> bool) (items: Loadable<StatefulItem<'T> array>) =
        
        items
        |> modify (fun items ->
            items |> Array.filter (fun item -> not <| StatefulItem.matches identifier item)
        )
        
    let update (identifier: 'T -> bool) (updatedItem: 'T) (items: Loadable<StatefulItem<'T> array>) =
        
        items
        |> modify (fun items -> items |> StatefulItems.update identifier updatedItem)
    
    let setDefault (identifier: 'T -> bool) (items: Loadable<StatefulItem<'T> array>) =
        
        items
        |> modify (fun items -> items |> StatefulItems.setDefault identifier)
    
    let setBusy (identifier: 'T -> bool) (items: Loadable<StatefulItem<'T> array>) =
        
        items
        |> modify (fun items -> items |> StatefulItems.setBusy identifier)
    
    let setError (identifier: 'T -> bool) (error: exn) (items: Loadable<StatefulItem<'T> array>) =
        
        items
        |> modify (fun items -> items |> StatefulItems.setError identifier error)
        
[<RequireQualifiedAccess>]
module LoadableStatefulItemsPage =
    
    let private modify (change: StatefulItem<'T> array -> StatefulItem<'T> array)
        (items: Loadable<Page<StatefulItem<'T>>>) =
        
        match items with
        | Loadable.Loaded page ->
            { page with items = change page.items }
            |> Loadable.Loaded
        
        | other -> other
        
    let without (identifier: 'T -> bool) (items: Loadable<Page<StatefulItem<'T>>>) =
        
        items
        |> modify (StatefulItems.without identifier)
        
    let update (identifier: 'T -> bool) (newValue: 'T) (items: Loadable<Page<StatefulItem<'T>>>) =
        
        items
        |> modify (StatefulItems.update identifier newValue)
        
    let setDefault (identifier: 'T -> bool) (items: Loadable<Page<StatefulItem<'T>>>) =
        
        items
        |> modify (StatefulItems.setDefault identifier)
        
    let setBusy (identifier: 'T -> bool) (items: Loadable<Page<StatefulItem<'T>>>) =
        
        items
        |> modify (StatefulItems.setBusy identifier)
        
    let setError (identifier: 'T -> bool) (error: exn) (items: Loadable<Page<StatefulItem<'T>>>) =
        
        items
        |> modify (StatefulItems.setError identifier error)
        
    let offset (items: Loadable<Page<StatefulItem<'T>>>) =
        
        match items with
        | Loadable.Loaded page -> page.offset / 50
        | _ -> 0
        
type State = {
    Page: Page
    Settings: Loadable<Settings>
    
    AddInspirationState: AddInspiration.State
    
    Inspirations: Loadable<StatefulItem<Inspiration> array>
    Prompts: Loadable<StatefulItem<Prompt> array>
    LocalDeviations: Loadable<Page<StatefulItem<LocalDeviation>>>
    StashedDeviations: Loadable<StatefulItem<StashedDeviation> array>
    PublishedDeviations: Loadable<StatefulItem<PublishedDeviation> array>
}

[<RequireQualifiedAccess>]
module State =
    
    let empty = {
        Page = Page.Home
        Settings = Loadable.NotLoaded
        
        AddInspirationState = AddInspiration.State.empty
        
        Inspirations = Loadable.NotLoaded
        Prompts = Loadable.NotLoaded
        LocalDeviations = Loadable.NotLoaded
        StashedDeviations = Loadable.NotLoaded
        PublishedDeviations = Loadable.NotLoaded
    }
    
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
    | LoadLocalDeviationsPage of offset: int * limit: int
    | LoadedLocalDeviationsPage of Loadable<Page<StatefulItem<LocalDeviation>>>
    
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
    | Inspiration2PromptFailed of Inspiration * promptText: string * error: exn
    
    | AddPrompt of promptText: string
    | AddedPrompt of Prompt
    | AddPromptFailed of promptText: string * error: exn
    
    | UpdatePrompt of Prompt
    | UpdatedPrompt of Prompt
    | UpdatePromptFailed of Prompt * error: exn
    
    | RemovePrompt of Prompt
    | ForgetPrompt of Prompt
    
    | Prompt2LocalDeviation of Prompt * Image
    | Prompt2LocalDeviationDone of Prompt * local: LocalDeviation
    | Prompt2LocalDeviationFailed of Prompt * Image * error: exn
    
    | AddedLocalDeviation of local: LocalDeviation
    
    | UpdateLocalDeviation of local: LocalDeviation
    
    | RemoveLocalDeviation of local: LocalDeviation
    | ForgetLocalDeviation of local: LocalDeviation
    
    | StashDeviation of local: LocalDeviation
    | StashedDeviation of local: LocalDeviation * stashed: StashedDeviation
    | StashDeviationFailed of local: LocalDeviation * error: exn
    
    | AddedStashedDeviation of stashed: StashedDeviation
    | RemoveStashedDeviation of stashed: StashedDeviation
    
    | PublishStashed of stashed: StashedDeviation
    | PublishedStashed of stashed: StashedDeviation * published: PublishedDeviation
    | PublishStashedFailed of stashed: StashedDeviation * error: exn
    
    | AddedPublishedDeviation of published: PublishedDeviation
