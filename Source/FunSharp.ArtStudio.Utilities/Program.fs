open System
open System.IO
open System.Net.Http
open System.Threading.Tasks
open FunSharp.Common
open FunSharp.Data
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.Api.Model
open FunSharp.ArtStudio.Model
open FunSharp.ArtStudio.Utilities
open FunSharp.OpenAI.Api.Model.Sora
open FunSharp.OpenAI.Api.Sora

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
    
    let realDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence.db"
    let copyDatabasePath = @"C:\Files\FunSharp.DeviantArt\persistence_copy.db"
    
    if not <| File.Exists(copyDatabasePath) then File.Copy(realDatabasePath, copyDatabasePath)
    
    use persistence = new NewLiteDbPersistence(copyDatabasePath) :> IPersistence
    use httpClient = new HttpClient()
    
    let secrets = Secrets.load()
    
    use client = new FunSharp.DeviantArt.Api.Client(persistence, httpClient, secrets.client_id, secrets.client_secret)
    
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
    
    printfn $"{JsonSerializer.serialize snippet}"
    
let genImageTest () =
    
    let variant = ImageType.Square
    let prompt =
        """
        cartoonish 3D (with cel-shading)
        cat baker
        baking a ton of "chocolate chip cookies"
        """
        
    let client = Client()
    
    let result =
        client.UpdateAuthTokens()
        |> Async.bind(fun () -> client.CreateImage(prompt, variant))
        |> Async.RunSynchronously
    
    printfn $"{result}"
    
let checkTaskTest () =
    
    let client = Client()
    
    let result =
        client.UpdateAuthTokens()
        |> Async.bind (fun () -> client.CheckTask("task_01k6z3hn4efpgrcyghxxddqcg3"))
        |> Async.RunSynchronously
        
    printfn $"{result}"
    
let getTasksTest () =
    
    let client = Client()
    
    let result =
        client.UpdateAuthTokens()
        |> Async.bind (fun () -> client.GetTasks())
        |> Async.RunSynchronously

    printfn $"%A{result}"
    
let deleteAllGenerationsForever () =
    
    let client = Client()
    
    let result =
        client.UpdateAuthTokens()
        |> Async.bind (fun () -> client.GetTasks())
        |> Async.bind(fun tasks ->
            tasks
            |> Array.collect (fun task -> [| for generation in task.generations do generation.id |])
            |> client.DeleteGenerations
        )
        |> Async.RunSynchronously

    printfn $"{result}"
    
let getTasksWithGenerationsTest () =
    
    let client = Client()
    
    let result =
        client.UpdateAuthTokens()
        |> Async.bind (fun () -> client.GetTasks())
        |> Async.RunSynchronously

    printfn $"%A{result |> Array.filter (fun x -> x.generations.Length > 0)}"
    
let fullProcessTest () =
    
    let client = Client()
    
    async {
        do! client.UpdateAuthTokens()
        
        let! taskId = client.CreateImage("a stunningly beautiful young woman inviting the viewer into her home, pov, hyperrealistic", ImageType.Portrait)
        printfn "task started!"
        
        let mutable taskDetails = TaskDetails.empty
        let mutable taskIsDone = false
        while (not taskIsDone) do
            let! newTaskDetails = client.CheckTask(taskId)
            taskDetails <- newTaskDetails
            
            if taskDetails.status = TaskStatus.Running || taskDetails.status = TaskStatus.PreProcessing then
                do! Task.Delay(5000)
                printfn "waiting..."
            else
                printfn "waiting is done!"
                taskIsDone <- true
                
        for generation in taskDetails.generations do
            let fileName = $"C:/Files/FunSharp.DeviantArt/automated/{Guid.NewGuid ()}.png"
            let! fileContent = client.DownloadImage(generation.url)
            do! FunSharp.Common.File.writeAllBytesAsync fileName fileContent
    }

let adHocTest () =
    
    let json = """
{
	"id": "task_01k7220wfhe3ea77qnkws68q78",
	"user": "user-aoBlrIMfjPcORklGBKDpQagL",
	"created_at": "2025-10-08T14:01:24.772665Z",
	"status": "preprocessing",
	"progress_pct": null,
	"progress_pos_in_queue": null,
	"estimated_queue_wait_time": null,
	"queue_status_message": null,
	"priority": 2,
	"type": "image_gen",
	"prompt": "a stunningly beautiful young woman inviting the viewer into her bedroom, pov, hyperrealistic, she is slim and very exceptionally stacked (really exaggerated)",
	"n_variants": 2,
	"n_frames": 1,
	"height": 720,
	"width": 480,
	"model": null,
	"operation": "simple_compose",
	"inpaint_items": [],
	"preset_id": null,
	"caption": null,
	"actions": null,
	"interpolation": null,
	"sdedit": null,
	"remix_config": null,
	"quality": null,
	"size": null,
	"generations": [],
	"num_unsafe_generations": 0,
	"title": "Image Generation",
	"moderation_result": {
		"type": "passed",
		"results_by_frame_index": {},
		"code": null,
		"is_output_rejection": false,
		"task_id": "task_01k7220wfhe3ea77qnkws68q78"
	},
	"failure_reason": null,
	"needs_user_review": false
}
"""
    
    let result = JsonSerializer.deserialize<TaskDetails> json
    
    printfn $"{result}"

[<EntryPoint>]
let main _ =
    
    adHocTest ()
    
    // migrateToTimestamps ()
    //testNewDb ()
    
    // testNewApiClient()

    // snippetTest ()
    
    // genImageTest ()
    
    // checkTaskTest ()
    
    // getTasksTest ()
    
    // deleteAllGenerationsForever ()
    
    // getTasksWithGenerationsTest ()
    
    // fullProcessTest () |> Async.RunSynchronously
    
    0
