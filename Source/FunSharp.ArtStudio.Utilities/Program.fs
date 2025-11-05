open System
open System.IO
open System.Net.Http
open System.Text.Json
open FunSharp.Common
open FunSharp.Common.JsonSerializer
open FunSharp.Data
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.Api.Model
open FunSharp.OpenAI.Api.Sora
open FunSharp.OpenAI.Api.Model.Sora
open FunSharp.ArtStudio.Model
open FunSharp.ArtStudio.Utilities
open FunSharp.ArtStudio.Server.Helpers

let adHocTest () =
    
    let json = """
{"id":"task_01k862djr2era887swgvhxxncn","user":"user-aoBlrIMfjPcORklGBKDpQagL","created_at":"2025-10-22T13:41:00.335618Z","status":"queued","progress_pct":null,"progress_pos_in_queue":null,"estimated_queue_wait_time":null,"queue_status_message":null,"priority":1,"type":"image_gen","prompt":"a stunningly beautiful young woman inviting the viewer into her bedroom, pov, hyperrealistic","n_variants":2,"n_frames":1,"height":720,"width":480,"model":null,"operation":"simple_compose","inpaint_items":[],"preset_id":null,"caption":null,"actions":null,"interpolation":null,"sdedit":null,"remix_config":null,"quality":null,"size":null,"generations":[],"num_unsafe_generations":0,"title":"Image Generation","moderation_result":{"type":"passed","results_by_frame_index":{},"code":null,"is_output_rejection":false,"task_id":"task_01k862djr2era887swgvhxxncn"},"failure_reason":null,"needs_user_review":false}
"""
    
    let optionsCustomizer (options: JsonSerializerOptions) =
        options.Converters.Add(NullTolerantFloatConverter())
        options.Converters.Add(CaseInsensitiveEnumConverter<TaskStatus>())
        options.Converters.Add(CaseInsensitiveEnumConverter<ModerationStatus>())
        
        options
    
    let result = customDeserialize<TaskDetails> optionsCustomizer json
    
    printfn $"{result}"

// let migrateToTimestamps () =
//
//     let realDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence.db"
//     
//     let copyDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence_copy.db"
//     let newDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence_new.db"
//     
//     if not <| File.Exists(copyDatabasePath) then
//         File.Copy(realDatabasePath, copyDatabasePath)
//     
//     use oldPersistence = new PickledPersistence(copyDatabasePath) :> IPersistence
//     use newPersistence = new NewLiteDbPersistence(newDatabasePath) :> IPersistence
//     
//     printfn "Migration started!"
//     
//     printfn "Migrating published deviations..."
//     oldPersistence.FindAll<PublishedDeviation>(dbKey_PublishedDeviations)
//     |> fun x ->
//         printfn $"items: {x.Length}"
//         x
//     |> Array.iter (fun oldItem ->
//         let newItem : NewPublishedDeviation = {
//             ImageUrl = oldItem.ImageUrl
//             Timestamp = DateTimeOffset.MinValue
//             Url = oldItem.Url
//             Origin =
//                 match oldItem.Origin with
//                 | DeviationOrigin.None -> NewDeviationOrigin.None
//                 | DeviationOrigin.Inspiration inspiration ->
//                     NewDeviationOrigin.Inspiration {
//                         ImageUrl = inspiration.ImageUrl
//                         Timestamp = DateTimeOffset.MinValue
//                         Url = inspiration.Url
//                     }
//                 | DeviationOrigin.Prompt prompt ->
//                     NewDeviationOrigin.Prompt {
//                         Id = prompt.Id
//                         Timestamp = DateTimeOffset.MinValue
//                         Text = prompt.Text
//                         Inspiration = prompt.Inspiration |> Option.map (fun x -> {
//                             ImageUrl = x.ImageUrl
//                             Timestamp = DateTimeOffset.MinValue
//                             Url = x.Url
//                         })
//                     }
//             Metadata = oldItem.Metadata
//         }
//         
//         newPersistence.Insert(dbKey_PublishedDeviations, newItem.ImageUrl, newItem)
//     )
//     
//     printfn "Migrating stashed deviations..."
//     oldPersistence.FindAll<StashedDeviation>(dbKey_StashedDeviations)
//     |> fun x ->
//         printfn $"items: {x.Length}"
//         x
//     |> Array.iter (fun oldItem ->
//         let newItem : NewStashedDeviation = {
//             ImageUrl = oldItem.ImageUrl
//             Timestamp = DateTimeOffset.MinValue
//             StashId = oldItem.StashId
//             Origin =
//                 match oldItem.Origin with
//                 | DeviationOrigin.None -> NewDeviationOrigin.None
//                 | DeviationOrigin.Inspiration inspiration ->
//                     NewDeviationOrigin.Inspiration {
//                         ImageUrl = inspiration.ImageUrl
//                         Timestamp = DateTimeOffset.MinValue
//                         Url = inspiration.Url
//                     }
//                 | DeviationOrigin.Prompt prompt ->
//                     NewDeviationOrigin.Prompt {
//                         Id = prompt.Id
//                         Timestamp = DateTimeOffset.MinValue
//                         Text = prompt.Text
//                         Inspiration = prompt.Inspiration |> Option.map (fun x -> {
//                             ImageUrl = x.ImageUrl
//                             Timestamp = DateTimeOffset.MinValue
//                             Url = x.Url
//                         })
//                     }
//             Metadata = oldItem.Metadata
//         }
//         
//         newPersistence.Insert(dbKey_StashedDeviations, newItem.ImageUrl, newItem)
//     )
//     
//     printfn "Migrating local deviations..."
//     oldPersistence.FindAll<LocalDeviation>(dbKey_LocalDeviations)
//     |> fun x ->
//         printfn $"items: {x.Length}"
//         x
//     |> Array.iter (fun oldItem ->
//         let newItem : NewLocalDeviation = {
//             ImageUrl = oldItem.ImageUrl
//             Timestamp = DateTimeOffset.MinValue
//             Origin =
//                 match oldItem.Origin with
//                 | DeviationOrigin.None -> NewDeviationOrigin.None
//                 | DeviationOrigin.Inspiration inspiration ->
//                     NewDeviationOrigin.Inspiration {
//                         ImageUrl = inspiration.ImageUrl
//                         Timestamp = DateTimeOffset.MinValue
//                         Url = inspiration.Url
//                     }
//                 | DeviationOrigin.Prompt prompt ->
//                     NewDeviationOrigin.Prompt {
//                         Id = prompt.Id
//                         Timestamp = DateTimeOffset.MinValue
//                         Text = prompt.Text
//                         Inspiration = prompt.Inspiration |> Option.map (fun x -> {
//                             ImageUrl = x.ImageUrl
//                             Timestamp = DateTimeOffset.MinValue
//                             Url = x.Url
//                         })
//                     }
//             Metadata = oldItem.Metadata
//         }
//         
//         newPersistence.Insert(dbKey_LocalDeviations, newItem.ImageUrl, newItem)
//     )
//     
//     printfn "Migrating prompts..."
//     oldPersistence.FindAll<Prompt>(dbKey_Prompts)
//     |> fun x ->
//         printfn $"items: {x.Length}"
//         x
//     |> Array.iter (fun oldItem ->
//         let newItem : NewPrompt = {
//             Id = oldItem.Id
//             Timestamp = DateTimeOffset.MinValue
//             Text = oldItem.Text
//             Inspiration = oldItem.Inspiration |> Option.map (fun x -> {
//                 ImageUrl = x.ImageUrl
//                 Timestamp = DateTimeOffset.MinValue
//                 Url = x.Url
//             })
//         }
//         
//         newPersistence.Insert(dbKey_Prompts, newItem.Id, newItem)
//     )
//     
//     printfn "Migrating inspirations..."
//     oldPersistence.FindAll<Inspiration>(dbKey_Inspirations)
//     |> fun x ->
//         printfn $"items: {x.Length}"
//         x
//     |> Array.iter (fun oldItem ->
//         let newItem : NewInspiration = {
//             Url = oldItem.Url
//             Timestamp = DateTimeOffset.MinValue
//             ImageUrl = oldItem.ImageUrl
//         }
//         
//         newPersistence.Insert(dbKey_Inspirations, newItem.Url, newItem)
//     )
//     
//     printfn "Migration done!"
//     
// let testNewDb () =
//     
//     use persistence = new NewLiteDbPersistence(@"C:\Files\FunSharp.DeviantArt\persistence.db") :> IPersistence
//     
//     persistence.FindAll<LocalDeviation>(dbKey_LocalDeviations)
//     |> fun x -> printfn $"items: {x.Length}"

[<RequireQualifiedAccess>]
module LookForCreator =
    
    let find<'T when 'T: not struct and 'T: not null> (persistence: IPersistence) (dbKey: string) (getOrigin: 'T -> DeviationOrigin) (keyOf: 'T -> string) (searchText: string) =
        
        persistence.FindAll<'T>(dbKey)
        |> Array.choose(fun item ->
            match DeviationOrigin.inspiration (getOrigin item) with
            | Some inspiration -> (keyOf item, inspiration.Url.ToString()) |> Some
            | None -> None
        )
        |> Array.filter (fun (_, inspiration) ->
            inspiration.ToLower().Contains(searchText.ToLower())
        )
        |> Array.iter (fun (key, inspiration) -> printfn $"{key} based on {inspiration}")

let lookForCreator (creator: string) =
    
    use persistence = new NewLiteDbPersistence(@"C:\Files\FunSharp.DeviantArt\test\persistence.db") :> IPersistence
    
    LookForCreator.find<PublishedDeviation> persistence dbKey_PublishedDeviations _.Origin (fun i -> (PublishedDeviation.keyOf i).ToString()) creator
    
    LookForCreator.find<LocalDeviation> persistence dbKey_LocalDeviations _.Origin (fun i -> (LocalDeviation.keyOf i).ToString()) creator
    
    persistence.FindAll<Prompt>(dbKey_Prompts)
        |> Array.choose(fun item ->
            match item.Inspiration with
            | Some inspiration -> Some ((Prompt.keyOf item).ToString(), inspiration.Url.ToString())
            | None -> None
        )
        |> Array.filter (fun (_, inspiration) ->
            inspiration.ToLower().Contains(creator.ToLower())
        )
        |> Array.iter (fun (key, inspiration) -> printfn $"prompt {key} based on {inspiration}")
        
    persistence.FindAll<Inspiration>(dbKey_Inspirations)
        |> Array.map(fun item ->
            (Inspiration.keyOf item).ToString()
        )
        |> Array.filter _.ToLower().Contains(creator.ToLower())
        |> Array.iter (fun inspiration -> printfn $"inspiration {inspiration}")

let testNewApiClient () =
    
    let realDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence.db"
    let copyDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence_copy.db"
    
    if not <| File.Exists(copyDatabasePath) then File.Copy(realDatabasePath, copyDatabasePath)
    
    use persistence = new NewLiteDbPersistence(copyDatabasePath) :> IPersistence
    use httpClient = new HttpClient()
    
    let secrets = Secrets.load()
    
    use client = new FunSharp.DeviantArt.Api.Client(persistence, secrets.client_id, secrets.client_secret)
    
    if client.NeedsInteraction then
        client.StartInteractiveLogin() |> Async.RunSynchronously
    
    let userInfo = client.WhoAmI() |> Async.RunSynchronously
    
    printfn $"Hello, {userInfo.username}!"
    
let snippetTest () =
    let snippet = {
      label = "test"
      value = "blob bla"
      action = SnippetAction.Append Paragraph.First
    }
    
    printfn $"{serialize snippet}"
    
let genImageTest () =
    
    let variant = AspectRatio.Square
    let prompt =
        """
        cartoonish 3D (with cel-shading)
        cat baker
        baking a ton of "chocolate chip cookies"
        """
        
    let client = new Client()
    
    let result =
        client.UpdateAuthTokens()
        |> Async.bind(fun () -> client.CreateImage(prompt, variant))
        |> Async.RunSynchronously
    
    printfn $"{result}"
            
    (client :> IDisposable).Dispose()
    
let checkTaskTest () =
    
    let client = new Client()
    
    let result =
        client.UpdateAuthTokens()
        |> Async.bind (fun () -> client.CheckTask("task_01k6z3hn4efpgrcyghxxddqcg3"))
        |> Async.RunSynchronously
        
    printfn $"{result}"
            
    (client :> IDisposable).Dispose()
    
let getTasksTest () =
    
    let client = new Client()
    
    let result =
        client.UpdateAuthTokens()
        |> Async.bind (fun () -> client.GetTasks())
        |> Async.RunSynchronously

    printfn $"%A{result}"
            
    (client :> IDisposable).Dispose()
    
let getTasksWithGenerationsTest () =
    
    let client = new Client()
    
    let result =
        client.UpdateAuthTokens()
        |> Async.bind (fun () -> client.GetTasks())
        |> Async.RunSynchronously

    printfn $"%A{result |> Array.filter (fun x -> x.generations.Length > 0)}"
            
    (client :> IDisposable).Dispose()
    
let fullProcessTest () =
    
    let client = new Client()
    
    let prompt = "a beautiful couple is camping out in the woods at night, pov from across the campfire, hyperrealistic"
    
    async {
        
        match! client.CreateImage(prompt, AspectRatio.Landscape) with
        | Ok result ->
            let files = result.Files |> String.concat ", "
            printfn $"files: {files}"
        | Error error ->
            printfn $"error: {error}"
            
        (client :> IDisposable).Dispose()
    }
    
let tasksCleanup () =
    
    let realDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence.db"
    
    use persistence = new NewLiteDbPersistence(realDatabasePath) :> IPersistence
    
    let soraTasks = persistence.FindAll<SoraTask>(dbKey_BackgroundTasks)
    
    for soraTask in soraTasks do
        let result = persistence.Delete(dbKey_BackgroundTasks, soraTask.Id.ToString())
        printfn $"{result}"
        persistence.Insert(dbKey_BackgroundTasks, soraTask.Id.ToString(), soraTask |> BackgroundTask.Sora)
        
    printfn "cleanup is done"

[<EntryPoint>]
let main _ =
    
    // adHocTest ()
    
    // migrateToTimestamps ()
    //testNewDb ()
    
    // lookForCreator "abc"
    
    // testNewApiClient()

    // snippetTest ()
    
    // genImageTest ()
    
    // checkTaskTest ()
    
    // getTasksTest ()
    
    // deleteAllGenerationsForever ()
    
    // getTasksWithGenerationsTest ()
    
    fullProcessTest () |> Async.RunSynchronously
    
    // tasksCleanup ()
    
    0
