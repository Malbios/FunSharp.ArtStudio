open System
open System.IO
open FunSharp.Data
open FunSharp.DeviantArt.Api.Model
open LiteDB
open MBrace.FsPickler

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

// Helper to resolve picklers correctly
let private resolve<'T> (r: IPicklerResolver) : Pickler<'T> =
    r.Resolve(typeof<'T>) :?> Pickler<'T>

// Graceful fallback for legacy/malformed DateTimeOffset values
let private safeReadDateTimeOffset (p: Pickler<DateTimeOffset>) (rs: ReadState) (tag: string) =
    try p.Read rs tag
    with _ -> DateTimeOffset.MinValue

// // Inspiration
// let makeInspirationPickler (r: IPicklerResolver) : Pickler<Inspiration> =
//     let uri = resolve<Uri> r
//     let dto = resolve<DateTimeOffset> r
//     let uriOpt = resolve<Option<Uri>> r
//
//     let writer (ws: WriteState) (x: Inspiration) =
//         uri.Write ws "url" x.Url
//         dto.Write ws "timestamp" x.Timestamp
//         uriOpt.Write ws "imageUrl" x.ImageUrl
//
//     let reader (rs: ReadState) =
//         let url = uri.Read rs "url"
//         let timestamp = safeReadDateTimeOffset dto rs "timestamp"
//         let imageUrl = uriOpt.Read rs "imageUrl"
//         { Url = url; Timestamp = timestamp; ImageUrl = imageUrl }
//
//     Pickler.FromPrimitives(reader, writer)
//
// // Prompt
// let makePromptPickler (r: IPicklerResolver) : Pickler<Prompt> =
//     let guid = resolve<Guid> r
//     let dto = resolve<DateTimeOffset> r
//     let str = resolve<string> r
//     let inspirationOpt = resolve<Option<Inspiration>> r
//
//     let writer (ws: WriteState) (x: Prompt) =
//         guid.Write ws "id" x.Id
//         dto.Write ws "timestamp" x.Timestamp
//         str.Write ws "text" x.Text
//         inspirationOpt.Write ws "inspiration" x.Inspiration
//
//     let reader (rs: ReadState) =
//         let id = guid.Read rs "id"
//         let timestamp = safeReadDateTimeOffset dto rs "timestamp"
//         let text = str.Read rs "text"
//         let inspiration = inspirationOpt.Read rs "inspiration"
//         { Id = id; Timestamp = timestamp; Text = text; Inspiration = inspiration }
//
//     Pickler.FromPrimitives(reader, writer)
//
// // LocalDeviation
// let makeLocalDeviationPickler (r: IPicklerResolver) : Pickler<LocalDeviation> =
//     let uri = resolve<Uri> r
//     let dto = resolve<DateTimeOffset> r
//     let origin = resolve<DeviationOrigin> r
//     let metadata = resolve<Metadata> r
//
//     let writer (ws: WriteState) (x: LocalDeviation) =
//         uri.Write ws "imageUrl" x.ImageUrl
//         dto.Write ws "timestamp" x.Timestamp
//         origin.Write ws "origin" x.Origin
//         metadata.Write ws "metadata" x.Metadata
//
//     let reader (rs: ReadState) =
//         let imageUrl = uri.Read rs "imageUrl"
//         let timestamp = safeReadDateTimeOffset dto rs "timestamp"
//         let origin = origin.Read rs "origin"
//         let metadata = metadata.Read rs "metadata"
//         { ImageUrl = imageUrl; Timestamp = timestamp; Origin = origin; Metadata = metadata }
//
//     Pickler.FromPrimitives(reader, writer)
//
// // StashedDeviation
// let makeStashedDeviationPickler (r: IPicklerResolver) : Pickler<StashedDeviation> =
//     let uri = resolve<Uri> r
//     let dto = resolve<DateTimeOffset> r
//     let i64 = resolve<int64> r
//     let origin = resolve<DeviationOrigin> r
//     let metadata = resolve<Metadata> r
//
//     let writer (ws: WriteState) (x: StashedDeviation) =
//         uri.Write ws "imageUrl" x.ImageUrl
//         dto.Write ws "timestamp" x.Timestamp
//         i64.Write ws "stashId" x.StashId
//         origin.Write ws "origin" x.Origin
//         metadata.Write ws "metadata" x.Metadata
//
//     let reader (rs: ReadState) =
//         let imageUrl = uri.Read rs "imageUrl"
//         let timestamp = safeReadDateTimeOffset dto rs "timestamp"
//         let stashId = i64.Read rs "stashId"
//         let origin = origin.Read rs "origin"
//         let metadata = metadata.Read rs "metadata"
//         { ImageUrl = imageUrl; Timestamp = timestamp; StashId = stashId; Origin = origin; Metadata = metadata }
//
//     Pickler.FromPrimitives(reader, writer)

type NewInspiration = {
    Url: Uri
    Timestamp: DateTime
    ImageUrl: Uri option
}

type NewPrompt = {
    Id: Guid
    Timestamp: DateTime
    Text: string
    Inspiration: Inspiration option
}

type NewLocalDeviation = {
    ImageUrl: Uri
    Timestamp: DateTime
    Origin: DeviationOrigin
    Metadata: Metadata
}

type NewStashedDeviation = {
    ImageUrl: Uri
    Timestamp: DateTime
    StashId: int64
    Origin: DeviationOrigin
    Metadata: Metadata
}

type NewPublishedDeviation = {
    ImageUrl: Uri
    Timestamp: DateTime
    Url: Uri
    Origin: DeviationOrigin
    Metadata: Metadata
}

// PublishedDeviation
// let makePublishedDeviationPickler (r: IPicklerResolver) : Pickler<PublishedDeviation> =
//     let uri = resolve<Uri> r
//     let dto = resolve<DateTimeOffset> r
//     let origin = resolve<DeviationOrigin> r
//     let metadata = resolve<Metadata> r
//
//     let writer (ws: WriteState) (x: PublishedDeviation) =
//         uri.Write ws "imageUrl" x.ImageUrl
//         dto.Write ws "timestamp" x.Timestamp
//         uri.Write ws "url" x.Url
//         origin.Write ws "origin" x.Origin
//         metadata.Write ws "metadata" x.Metadata
//
//     let reader (rs: ReadState) =
//         let imageUrl = uri.Read rs "imageUrl"
//         let timestamp = safeReadDateTimeOffset dto rs "timestamp"
//         let url = uri.Read rs "url"
//         let origin = origin.Read rs "origin"
//         let metadata = metadata.Read rs "metadata"
//         { ImageUrl = imageUrl; Timestamp = timestamp; Url = url; Origin = origin; Metadata = metadata }
//
//     Pickler.FromPrimitives(reader, writer)

[<EntryPoint>]
let main _ =

    let realDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence.db"
    let copyDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence_backup.db"
    
    // if File.Exists(copyDatabasePath) then File.Delete(copyDatabasePath)
    // File.Copy(realDatabasePath, copyDatabasePath)
    if File.Exists(copyDatabasePath) |> not then File.Copy(realDatabasePath, copyDatabasePath)
    
    let persistence = LiteDbPersistence(copyDatabasePath)

    let serializer = FsPickler.CreateBinarySerializer()
    // let publishedPickler = makePublishedDeviationPickler serializer.Resolver
    // let stashedPickler = makeStashedDeviationPickler serializer.Resolver
    // let localPickler = makeLocalDeviationPickler serializer.Resolver
    // let promptPickler = makePromptPickler serializer.Resolver
    // let inspirationPickler = makeInspirationPickler serializer.Resolver
    
    printfn "Migration started!"
    
    printfn "Migrating published deviations..."
    
    // persistence.FindAll<BsonDocument>(dbKey_PublishedDeviations)
    // |> Array.iter (fun doc ->
    //     let oldItem = serializer.UnPickle<PublishedDeviation>(doc["data"])
    //     // let oldItem = serializer.UnPickle<PublishedDeviation>(doc["data"], pickler = publishedPickler)
    //     persistence.Delete(dbKey_PublishedDeviations, oldItem.ImageUrl.ToString()) |> ignore
    //     let newItem : NewPublishedDeviation = { ImageUrl = oldItem.ImageUrl; Timestamp = DateTime.MinValue; Url = oldItem.Url; Origin = oldItem.Origin; Metadata = oldItem.Metadata }
    //     doc["data"] <- serializer.Pickle(newItem)
    //     // doc["data"] <- serializer.Pickle(newItem, pickler = publishedPickler)
    //     persistence.Insert(dbKey_PublishedDeviations, newItem.ImageUrl.ToString(), doc)
    // )
    //
    // persistence.FindAll<BsonDocument>(dbKey_PublishedDeviations)
    // |> Array.iter (fun doc ->
    //     let item = serializer.UnPickle<NewPublishedDeviation>(doc["data"])
    //     printfn $"{item.ImageUrl}"
    //     printfn $"{item.Timestamp}"
    // )
    
    // persistence.FindAll<BsonDocument>(dbKey_PublishedDeviations)
    // |> Array.iter (fun doc ->
    //     let oldItem = serializer.UnPickle<NewPublishedDeviation>(doc["data"])
    //     // let oldItem = serializer.UnPickle<PublishedDeviation>(doc["data"], pickler = publishedPickler)
    //     persistence.Delete(dbKey_PublishedDeviations, oldItem.ImageUrl.ToString()) |> ignore
    //     let newItem : PublishedDeviation = { ImageUrl = oldItem.ImageUrl; Timestamp = oldItem.Timestamp; Url = oldItem.Url; Origin = oldItem.Origin; Metadata = oldItem.Metadata }
    //     doc["data"] <- serializer.Pickle(newItem)
    //     persistence.Insert(dbKey_PublishedDeviations, newItem.ImageUrl.ToString(), doc)
    // )
    //
    // persistence.FindAll<BsonDocument>(dbKey_PublishedDeviations)
    // |> Array.iter (fun doc ->
    //     let item = serializer.UnPickle<PublishedDeviation>(doc["data"])
    //     printfn $"{item.ImageUrl}"
    //     printfn $"{item.Timestamp}"
    // )
    
    persistence.FindAll<BsonDocument>(dbKey_PublishedDeviations)
    |> Array.iter (fun doc ->
        let oldItem = serializer.UnPickle<PublishedDeviation>(doc["data"])
        persistence.Delete(dbKey_PublishedDeviations, oldItem.ImageUrl.ToString()) |> ignore
        let newItem : NewPublishedDeviation = { ImageUrl = oldItem.ImageUrl; Timestamp = DateTime.MinValue; Url = oldItem.Url; Origin = oldItem.Origin; Metadata = oldItem.Metadata }
        doc["data"] <- serializer.Pickle(newItem)
        persistence.Insert(dbKey_PublishedDeviations, newItem.ImageUrl.ToString(), doc)
    )
    
    persistence.FindAll<BsonDocument>(dbKey_StashedDeviations)
    |> Array.iter (fun doc ->
        let oldItem = serializer.UnPickle<StashedDeviation>(doc["data"])
        persistence.Delete(dbKey_StashedDeviations, oldItem.ImageUrl.ToString()) |> ignore
        let newItem : NewStashedDeviation = { ImageUrl = oldItem.ImageUrl; Timestamp = DateTime.MinValue; Url = oldItem.Url; Origin = oldItem.Origin; Metadata = oldItem.Metadata }
        doc["data"] <- serializer.Pickle(newItem)
        persistence.Insert(dbKey_StashedDeviations, newItem.ImageUrl.ToString(), doc)
    )
    
    persistence.FindAll<BsonDocument>(dbKey_PublishedDeviations)
    |> Array.iter (fun doc ->
        let oldItem = serializer.UnPickle<PublishedDeviation>(doc["data"])
        persistence.Delete(dbKey_PublishedDeviations, oldItem.ImageUrl.ToString()) |> ignore
        let newItem : NewPublishedDeviation = { ImageUrl = oldItem.ImageUrl; Timestamp = DateTime.MinValue; Url = oldItem.Url; Origin = oldItem.Origin; Metadata = oldItem.Metadata }
        doc["data"] <- serializer.Pickle(newItem)
        persistence.Insert(dbKey_PublishedDeviations, newItem.ImageUrl.ToString(), doc)
    )
    
    persistence.FindAll<BsonDocument>(dbKey_PublishedDeviations)
    |> Array.iter (fun doc ->
        let oldItem = serializer.UnPickle<PublishedDeviation>(doc["data"])
        persistence.Delete(dbKey_PublishedDeviations, oldItem.ImageUrl.ToString()) |> ignore
        let newItem : NewPublishedDeviation = { ImageUrl = oldItem.ImageUrl; Timestamp = DateTime.MinValue; Url = oldItem.Url; Origin = oldItem.Origin; Metadata = oldItem.Metadata }
        doc["data"] <- serializer.Pickle(newItem)
        persistence.Insert(dbKey_PublishedDeviations, newItem.ImageUrl.ToString(), doc)
    )
    
    persistence.FindAll<BsonDocument>(dbKey_PublishedDeviations)
    |> Array.iter (fun doc ->
        let oldItem = serializer.UnPickle<PublishedDeviation>(doc["data"])
        persistence.Delete(dbKey_PublishedDeviations, oldItem.ImageUrl.ToString()) |> ignore
        let newItem : NewPublishedDeviation = { ImageUrl = oldItem.ImageUrl; Timestamp = DateTime.MinValue; Url = oldItem.Url; Origin = oldItem.Origin; Metadata = oldItem.Metadata }
        doc["data"] <- serializer.Pickle(newItem)
        persistence.Insert(dbKey_PublishedDeviations, newItem.ImageUrl.ToString(), doc)
    )
    
    // ImageUrl: Uri
    // // Timestamp: DateTimeOffset
    // Url: Uri
    // Origin: DeviationOrigin
    // Metadata: Metadata
    
    // printfn "Migrating stashed deviations..."
    // persistence.FindAll<BsonDocument>(dbKey_StashedDeviations)
    // |> Array.iter (fun doc ->
    //     let oldItem = serializer.UnPickle<StashedDeviation>(doc["data"], pickler = stashedPickler)
    //     persistence.Delete(dbKey_StashedDeviations, oldItem.ImageUrl.ToString()) |> ignore
    //     let newItem = { oldItem with Timestamp = DateTimeOffset.MinValue }
    //     doc["data"] <- serializer.Pickle(newItem, pickler = stashedPickler)
    //     persistence.Insert(dbKey_StashedDeviations, newItem.ImageUrl.ToString(), doc)
    // )
    //
    // printfn "Migrating local deviations..."
    // persistence.FindAll<BsonDocument>(dbKey_LocalDeviations)
    // |> Array.iter (fun doc ->
    //     let oldItem = serializer.UnPickle<LocalDeviation>(doc["data"], pickler = localPickler)
    //     persistence.Delete(dbKey_LocalDeviations, oldItem.ImageUrl.ToString()) |> ignore
    //     let newItem = { oldItem with Timestamp = DateTimeOffset.MinValue }
    //     doc["data"] <- serializer.Pickle(newItem, pickler = localPickler)
    //     persistence.Insert(dbKey_LocalDeviations, newItem.ImageUrl.ToString(), doc)
    // )
    //
    // printfn "Migrating prompts..."
    // persistence.FindAll<BsonDocument>(dbKey_Prompts)
    // |> Array.iter (fun doc ->
    //     let oldItem = serializer.UnPickle<Prompt>(doc["data"], pickler = promptPickler)
    //     persistence.Delete(dbKey_Prompts, oldItem.Id.ToString()) |> ignore
    //     let newItem = { oldItem with Timestamp = DateTimeOffset.MinValue }
    //     doc["data"] <- serializer.Pickle(newItem, pickler = promptPickler)
    //     persistence.Insert(dbKey_Prompts, newItem.Id.ToString(), doc)
    // )
    //
    // printfn "Migrating inspirations..."
    // persistence.FindAll<BsonDocument>(dbKey_Inspirations)
    // |> Array.iter (fun doc ->
    //     let oldItem = serializer.UnPickle<Inspiration>(doc["data"], pickler = inspirationPickler)
    //     persistence.Delete(dbKey_Inspirations, oldItem.Url.ToString()) |> ignore
    //     let newItem = { oldItem with Timestamp = DateTimeOffset.MinValue }
    //     doc["data"] <- serializer.Pickle(newItem, pickler = inspirationPickler)
    //     persistence.Insert(dbKey_Inspirations, oldItem.Url.ToString(), doc)
    // )
    
    printfn "Migration done!"
    
    0
