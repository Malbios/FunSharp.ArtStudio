open System
open System.IO
open FunSharp.Data
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.Api.Model

[<Literal>]
let dbKey_Settings = "Settings"

[<Literal>]
let dbKey_Inspirations = "Inspirations"

[<Literal>]
let dbKey_Prompts = "Prompts"

[<Literal>]
let dbKey_LocalDeviations = "LocalDeviations"

[<Literal>]
let dbKey_StashedDeviations = "StashedDeviations"

[<Literal>]
let dbKey_PublishedDeviations = "PublishedDeviations"

type NewInspiration = {
    Url: Uri
    Timestamp: DateTimeOffset
    ImageUrl: Uri option
}

type NewPrompt = {
    Id: Guid
    Timestamp: DateTimeOffset
    Text: string
    Inspiration: NewInspiration option
}

[<RequireQualifiedAccess>]
type NewDeviationOrigin =
    | None
    | Prompt of NewPrompt
    | Inspiration of NewInspiration

type NewLocalDeviation = {
    ImageUrl: Uri
    Timestamp: DateTimeOffset
    Origin: NewDeviationOrigin
    Metadata: Metadata
}

type NewStashedDeviation = {
    ImageUrl: Uri
    Timestamp: DateTimeOffset
    StashId: int64
    Origin: NewDeviationOrigin
    Metadata: Metadata
}

type NewPublishedDeviation = {
    ImageUrl: Uri
    Timestamp: DateTimeOffset
    Url: Uri
    Origin: NewDeviationOrigin
    Metadata: Metadata
}

type NewSettings = {
    Galleries: Gallery array
}

let migrateToTimestamps () =

    let realDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence.db"
    
    let copyDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence_copy.db"
    let newDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence_new.db"
    
    if not <| File.Exists(copyDatabasePath) then
        File.Copy(realDatabasePath, copyDatabasePath)
    
    use oldPersistence = new PickledPersistence(copyDatabasePath) :> IPersistence
    use newPersistence = new NewLiteDbPersistence(newDatabasePath) :> IPersistence
    
    printfn "Migration started!"
    
    printfn "Migrating published deviations..."
    oldPersistence.FindAll<PublishedDeviation>(dbKey_PublishedDeviations)
    |> fun x ->
        printfn $"items: {x.Length}"
        x
    |> Array.iter (fun oldItem ->
        let newItem : NewPublishedDeviation = {
            ImageUrl = oldItem.ImageUrl
            Timestamp = DateTimeOffset.MinValue
            Url = oldItem.Url
            Origin =
                match oldItem.Origin with
                | DeviationOrigin.None -> NewDeviationOrigin.None
                | DeviationOrigin.Inspiration inspiration ->
                    NewDeviationOrigin.Inspiration {
                        ImageUrl = inspiration.ImageUrl
                        Timestamp = DateTimeOffset.MinValue
                        Url = inspiration.Url
                    }
                | DeviationOrigin.Prompt prompt ->
                    NewDeviationOrigin.Prompt {
                        Id = prompt.Id
                        Timestamp = DateTimeOffset.MinValue
                        Text = prompt.Text
                        Inspiration = prompt.Inspiration |> Option.map (fun x -> {
                            ImageUrl = x.ImageUrl
                            Timestamp = DateTimeOffset.MinValue
                            Url = x.Url
                        })
                    }
            Metadata = oldItem.Metadata
        }
        
        newPersistence.Insert(dbKey_PublishedDeviations, newItem.ImageUrl, newItem)
    )
    
    printfn "Migrating stashed deviations..."
    oldPersistence.FindAll<StashedDeviation>(dbKey_StashedDeviations)
    |> fun x ->
        printfn $"items: {x.Length}"
        x
    |> Array.iter (fun oldItem ->
        let newItem : NewStashedDeviation = {
            ImageUrl = oldItem.ImageUrl
            Timestamp = DateTimeOffset.MinValue
            StashId = oldItem.StashId
            Origin =
                match oldItem.Origin with
                | DeviationOrigin.None -> NewDeviationOrigin.None
                | DeviationOrigin.Inspiration inspiration ->
                    NewDeviationOrigin.Inspiration {
                        ImageUrl = inspiration.ImageUrl
                        Timestamp = DateTimeOffset.MinValue
                        Url = inspiration.Url
                    }
                | DeviationOrigin.Prompt prompt ->
                    NewDeviationOrigin.Prompt {
                        Id = prompt.Id
                        Timestamp = DateTimeOffset.MinValue
                        Text = prompt.Text
                        Inspiration = prompt.Inspiration |> Option.map (fun x -> {
                            ImageUrl = x.ImageUrl
                            Timestamp = DateTimeOffset.MinValue
                            Url = x.Url
                        })
                    }
            Metadata = oldItem.Metadata
        }
        
        newPersistence.Insert(dbKey_StashedDeviations, newItem.ImageUrl, newItem)
    )
    
    printfn "Migrating local deviations..."
    oldPersistence.FindAll<LocalDeviation>(dbKey_LocalDeviations)
    |> fun x ->
        printfn $"items: {x.Length}"
        x
    |> Array.iter (fun oldItem ->
        let newItem : NewLocalDeviation = {
            ImageUrl = oldItem.ImageUrl
            Timestamp = DateTimeOffset.MinValue
            Origin =
                match oldItem.Origin with
                | DeviationOrigin.None -> NewDeviationOrigin.None
                | DeviationOrigin.Inspiration inspiration ->
                    NewDeviationOrigin.Inspiration {
                        ImageUrl = inspiration.ImageUrl
                        Timestamp = DateTimeOffset.MinValue
                        Url = inspiration.Url
                    }
                | DeviationOrigin.Prompt prompt ->
                    NewDeviationOrigin.Prompt {
                        Id = prompt.Id
                        Timestamp = DateTimeOffset.MinValue
                        Text = prompt.Text
                        Inspiration = prompt.Inspiration |> Option.map (fun x -> {
                            ImageUrl = x.ImageUrl
                            Timestamp = DateTimeOffset.MinValue
                            Url = x.Url
                        })
                    }
            Metadata = oldItem.Metadata
        }
        
        newPersistence.Insert(dbKey_LocalDeviations, newItem.ImageUrl, newItem)
    )
    
    printfn "Migrating prompts..."
    oldPersistence.FindAll<Prompt>(dbKey_Prompts)
    |> fun x ->
        printfn $"items: {x.Length}"
        x
    |> Array.iter (fun oldItem ->
        let newItem : NewPrompt = {
            Id = oldItem.Id
            Timestamp = DateTimeOffset.MinValue
            Text = oldItem.Text
            Inspiration = oldItem.Inspiration |> Option.map (fun x -> {
                ImageUrl = x.ImageUrl
                Timestamp = DateTimeOffset.MinValue
                Url = x.Url
            })
        }
        
        newPersistence.Insert(dbKey_Prompts, newItem.Id, newItem)
    )
    
    printfn "Migrating inspirations..."
    oldPersistence.FindAll<Inspiration>(dbKey_Inspirations)
    |> fun x ->
        printfn $"items: {x.Length}"
        x
    |> Array.iter (fun oldItem ->
        let newItem : NewInspiration = {
            Url = oldItem.Url
            Timestamp = DateTimeOffset.MinValue
            ImageUrl = oldItem.ImageUrl
        }
        
        newPersistence.Insert(dbKey_Inspirations, newItem.Url, newItem)
    )
    
    printfn "Migration done!"
    
let testNewDb () =
    
    use persistence = new NewLiteDbPersistence(@"C:\Files\FunSharp.DeviantArt\persistence.db") :> IPersistence
    
    persistence.FindAll<LocalDeviation>(dbKey_LocalDeviations)
    |> fun x -> printfn $"items: {x.Length}"
    
let testNewApiClient () =
    
    let client = new FunSharp.DeviantArt.NewApi.Client(persistence, sender, clientId, clientSecret)

[<EntryPoint>]
let main _ =
    
    // migrateToTimestamps ()
    testNewDb ()
    
    0
