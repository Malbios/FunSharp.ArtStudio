namespace FunSharp.DeviantArt.Manager

open System
open System.IO
open System.Net.Http

open System.Net.Http.Headers
open Elmish
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager.Model
open MBrace.FsPickler
open Microsoft.AspNetCore.Components.Forms
open Microsoft.Extensions.Logging
open Newtonsoft.Json
open FunSharp.Common
open FunSharp.DeviantArt.Api

module Update =
    
    let private apiRoot = "http://localhost:5123/api/v1"
    
    // let private pickler = FsPickler.CreateBinarySerializer()
    //
    // type private DbItem = {
    //     Id: string
    //     Value: byte array
    // }
    //
    // let private modelWithUpdatedUploadedFile model key (update: UploadedFile -> UploadedFile) = {
    //     model with
    //         UploadedFiles = model.UploadedFiles
    //         |> Array.map (fun x ->
    //             if x.FileName = key then
    //                 update x
    //             else
    //                 x
    //         )
    // }
    //
    // let private processUpload (file: IBrowserFile) = async {
    //     let maxSize = 1024L * 1024L * 100L // 100 MB
    //     
    //     use stream = file.OpenReadStream(maxAllowedSize = maxSize)
    //     use ms = new MemoryStream()
    //     
    //     do! stream.CopyToAsync(ms)
    //     
    //     let byteArray = ms.ToArray()
    //     
    //     return (file.Name, $"data:{file.ContentType};base64,{Convert.ToBase64String(byteArray)}", byteArray)
    // }
    //
    // let private initDatabase (database: IndexedDb) =
    //     
    //     let stores = [|
    //         Common.dbKey_Settings
    //         Common.dbKey_Inspirations
    //         Common.dbKey_LocalDeviations
    //         Common.dbKey_StashedDeviations
    //         Common.dbKey_PublishedDeviations
    //     |]
    //     
    //     database.Init(Common.dbName, stores)
    //
    // let private loadDeviations<'T> (logger: ILogger) (database: IndexedDb) storeName=
    //     
    //     logger.LogInformation $"loading deviations from {storeName}..."
    //     
    //     initDatabase database
    //     |> Async.catch
    //     |> AsyncResult.getOrFail
    //     |> Async.bind (fun () ->
    //         database.GetAll<string, byte array>(storeName)
    //         |> Async.map (fun x -> x |> Array.map (fun (_, value) -> pickler.UnPickle<'T> value))
    //     )
    //     |> Async.catch
    //     |> AsyncResult.getOrFail
    //
    // let private loadLocalDeviations logger database =
    //     loadDeviations<UploadedFile> logger database Common.dbKey_LocalDeviations
    //
    // let private loadStashedDeviations logger database =
    //     loadDeviations<StashedDeviation> logger database Common.dbKey_StashedDeviations
    //
    // let private loadPublishedDeviations logger database =
    //     loadDeviations<PublishedDeviation> logger database Common.dbKey_PublishedDeviations
    //     
    // let private saveItem<'T> (logger: ILogger) (database: IndexedDb) storeName key (value: 'T) =
    //     
    //     logger.LogInformation $"saving deviation to '{storeName}': {key}"
    //     
    //     let value = pickler.Pickle value
    //     
    //     initDatabase database
    //     |> Async.bind (fun () ->
    //         database.Set(storeName, key, value)
    //     )
    //     
    // let private saveLocalDeviation logger database key (value: UploadedFile) =
    //     saveItem logger database Common.dbKey_LocalDeviations key value
    //     
    // let private saveStashedDeviation logger database key (value: StashedDeviation) =
    //     saveItem logger database Common.dbKey_StashedDeviations key value
    //     
    // let private savePublishedDeviations logger database key (value: StashedDeviation) =
    //     saveItem logger database Common.dbKey_PublishedDeviations key value
    //     
    // let private deleteDeviation<'T> (logger: ILogger) (database: IndexedDb) storeName key =
    //     
    //     logger.LogInformation $"deleting deviation in '{storeName}': {key}"
    //     
    //     initDatabase database
    //     |> Async.bind (fun () ->
    //         database.Delete(storeName, key)
    //     )
    //     
    // let private deleteLocalDeviation logger database =
    //     deleteDeviation logger database Common.dbKey_LocalDeviations
    //     
    // let private deleteStashedDeviation logger database =
    //     deleteDeviation logger database Common.dbKey_StashedDeviations
    //     
    // let private deletePublishedDeviations logger database =
    //     deleteDeviation logger database Common.dbKey_PublishedDeviations
    //     
    // let private processStashSubmission file (response: HttpResponseMessage) =
    //     
    //     response.Content.ReadAsStringAsync()
    //     |> Async.AwaitTask
    //     |> Async.catch
    //     |> AsyncResult.getOrFail
    //     |> Async.map (fun content ->
    //         let response =
    //             content |> JsonSerializer.deserialize<ApiResponses.StashSubmission>
    //             
    //         match response.status with
    //         | "success" ->
    //             let submission = {
    //                 StashId = response.item_id
    //                 Metadata = file.Metadata
    //             }
    //             
    //             (file, submission)
    //         | _ ->
    //             failwith $"Failed to stash {file.FileName}"
    //     )
    //     
    // let private submitToStash (logger: ILogger) (client: HttpClient) file =
    //     
    //     logger.LogInformation $"Stashing {file.FileName}..."
    //     logger.LogInformation $"{file.Metadata |> JsonConvert.SerializeObject}"
    //     
    //     let url = $"{apiRoot}/stash"
    //     
    //     let byteContent = new ByteArrayContent(file.Content)
    //     
    //     use content = new MultipartFormDataContent()
    //     byteContent.Headers.ContentType <- MediaTypeHeaderValue("image/png")
    //     content.Add(byteContent, "file", file.FileName)
    //     content.Add(new StringContent(file.Metadata.Title), "title")
    //
    //     client.PostAsync(url, content)
    //     |> Async.AwaitTask
    //     |> Async.tee (fun response -> response.EnsureSuccessStatusCode() |> ignore)
    //     |> Async.catch
    //     |> AsyncResult.getOrFail
    //     |> Async.bind (fun response -> processStashSubmission file response)
    
    let loadItems<'T> (client: HttpClient) (endpoint: string) (asLoadable: string -> Loadable<'T>) =
        
        client.GetAsync($"{apiRoot}{endpoint}")
        |> Async.AwaitTask
        |> Async.tee (fun response -> response.EnsureSuccessStatusCode() |> ignore)
        |> Async.catch
        |> AsyncResult.getOrFail
        |> Async.bind (fun response ->
            response.Content.ReadAsStringAsync()
            |> Async.AwaitTask
            |> Async.catch
            |> AsyncResult.getOrFail
            |> Async.map asLoadable
        )
    
    let loadInspirations (client: HttpClient) =
        
        (JsonSerializer.deserialize<Inspiration array> >> Loadable.Loaded)
        |> loadItems client "/inspirations"
    
    let loadPrompts (client: HttpClient) =
        
        (JsonSerializer.deserialize<Prompt array> >> Loadable.Loaded)
        |> loadItems client "/prompts"
    
    let loadLocalDeviations (client: HttpClient) =
        
        (JsonSerializer.deserialize<LocalDeviation array> >> Loadable.Loaded)
        |> loadItems client "/local-deviations"
    
    let loadStashedDeviations (client: HttpClient) =
        
        (JsonSerializer.deserialize<StashedDeviation array> >> Loadable.Loaded)
        |> loadItems client "/stashed-deviations"
    
    let loadPublishedDeviations (client: HttpClient) =
        
        (JsonSerializer.deserialize<PublishedDeviation array> >> Loadable.Loaded)
        |> loadItems client "/published-deviations"
    
    let update (_: ILogger) (client: HttpClient) message (model: Model.State) =
    
        match message with
        
        | SetPage page ->
            { model with Page = page }, Cmd.none

        | Initialize ->
            
            let batch = Cmd.batch [
                Cmd.ofMsg LoadInspirations
                Cmd.ofMsg LoadPrompts
                Cmd.ofMsg LoadLocalDeviation
                Cmd.ofMsg LoadStashedDeviation
                Cmd.ofMsg LoadPublishedDeviation
            ]
            
            model, batch
        
        | LoadInspirations ->
            
            let load () = loadInspirations client
            let failed ex = LoadedInspirations (Loadable.LoadingFailed ex)
            
            { model with Inspirations = Loading }, Cmd.OfAsync.either load () LoadedInspirations failed
            
        | LoadedInspirations loadable ->
            { model with Inspirations = loadable }, Cmd.none

        | LoadPrompts ->
            
            let load () = loadPrompts client
            let failed ex = LoadedPrompts (Loadable.LoadingFailed ex)
            
            { model with Prompts = Loading }, Cmd.OfAsync.either load () LoadedPrompts failed

        | LoadedPrompts loadable ->
            { model with Prompts = loadable }, Cmd.none

        | LoadLocalDeviation ->
            
            let load () = loadLocalDeviations client
            let failed ex = LoadedLocalDeviation (Loadable.LoadingFailed ex)
            
            { model with LocalDeviations = Loading }, Cmd.OfAsync.either load () LoadedLocalDeviation failed

        | LoadedLocalDeviation loadable ->
            { model with LocalDeviations = loadable }, Cmd.none

        | LoadStashedDeviation ->
            
            let load () = loadStashedDeviations client
            let failed ex = LoadedStashedDeviation (Loadable.LoadingFailed ex)
            
            { model with StashedDeviations = Loading }, Cmd.OfAsync.either load () LoadedStashedDeviation failed

        | LoadedStashedDeviation loadable ->
            { model with StashedDeviations = loadable }, Cmd.none

        | LoadPublishedDeviation ->
            let load () = loadPublishedDeviations client
            let failed ex = LoadedPublishedDeviation (Loadable.LoadingFailed ex)
            
            { model with PublishedDeviations = Loading }, Cmd.OfAsync.either load () LoadedPublishedDeviation failed

        | LoadedPublishedDeviation loadable ->
            { model with PublishedDeviations = loadable }, Cmd.none
        
        | AddInspiration inspiration ->
            failwith "todo"
            
        | Inspiration2Prompt (inspiration, prompt) ->
            failwith "todo"
            
        | Prompt2LocalDeviation (prompt, metadata) ->
            failwith "todo"
        
        | StashDeviation metadata ->
            failwith "todo"
        
        | PublishStashed stashedDeviation ->
            failwith "todo"
        
        | UploadLocalDeviations browserFiles ->
            failwith "todo"
            
        // | UploadLocalDeviations newFiles ->
        //     
        //     let alreadyExists (file: IBrowserFile) =
        //         model.UploadedFiles |> Array.exists (fun x -> x.FileName = file.Name)
        //         
        //     let newFiles =
        //         newFiles
        //         |> Array.filter (fun x -> alreadyExists x |> not)
        //     
        //     let processUploadCommands =
        //         newFiles
        //         |> Array.map (fun x -> Cmd.OfAsync.perform processUpload x UploadedFiles)
        //         
        //     let newFiles =
        //         newFiles
        //         |> Array.map (fun x -> { UploadedFile.empty with FileName = x.Name })
        //         
        //     let consolidatedFiles =
        //         [model.UploadedFiles; newFiles] |> Array.concat
        //     
        //     { model with UploadedFiles = consolidatedFiles }, Cmd.batch processUploadCommands
        //
        // | UploadedFiles (fileName, previewUrl, content) ->
        //     
        //     let update x =
        //         { x with ImageUrl = previewUrl; Content = content }
        //     
        //     modelWithUpdatedUploadedFile model fileName update, Cmd.none
        //
        // | UpdateUploadedFile file ->
        //     
        //     let model = modelWithUpdatedUploadedFile model file.FileName (fun _ -> file)
        //     
        //     model, Cmd.none

        // | LoadDeviations ->
        //     
        //     let loadAllDeviations () =
        //         loadLocalDeviations logger database
        //         |> Async.bind (fun local ->
        //             loadStashedDeviations logger database
        //             |> Async.map (fun stashed ->
        //                 (local, stashed)
        //             )
        //         )
        //         |> Async.bind (fun (local, stashed) ->
        //             loadPublishedDeviations logger database
        //             |> Async.map (fun published ->
        //                 (local, stashed, published)
        //             )
        //         )
        //     
        //     { model with IsBusy = true }, Cmd.OfAsync.either loadAllDeviations () LoadedDeviations Error
        //     
        // | LoadedDeviations(local, stashed, published) ->
        //         
        //     let model =
        //         {
        //             model with
        //                 UploadedFiles = local
        //                 StashedDeviations = stashed
        //                 PublishedDeviations = published
        //         }
        //     
        //     logger.LogInformation $"Loaded {local.Length} uploaded, {stashed.Length} stashed and {published.Length} published deviations."
        //     
        //     model, Cmd.ofMsg Done
        //     
        // | SaveUploadedFile file ->
        //     
        //     let save = saveLocalDeviation logger database file.FileName
        //     
        //     { model with IsBusy = true }, Cmd.OfAsync.either save file (fun () -> Done) Error
        //
        // | SaveStashedFile file ->
        //     
        //     let save = saveStashedDeviation logger database (file.StashId.ToString())
        //     
        //     { model with IsBusy = true }, Cmd.OfAsync.either save file (fun () -> Done) Error
        //
        // | DeleteLocalFile file ->
        //     
        //     let model = {
        //         model with
        //             UploadedFiles = model.UploadedFiles |> Array.filter (fun x -> x.FileName <> file.FileName)
        //     }
        //     
        //     let delete = deleteLocalDeviation logger database
        //     
        //     model, Cmd.OfAsync.either delete file.FileName (fun () -> Done) Error
        //
        // | Stash file ->
        //     
        //     let gallery = file.Metadata.Gallery
        //     
        //     let isMature =
        //         match Helpers.gallery gallery with
        //         | Helpers.Gallery.Spicy -> true
        //         | _ -> false
        //     
        //     let file = {
        //         file with
        //             Metadata.Gallery = Helpers.galleryId gallery
        //             Metadata.IsMature = isMature
        //     }
        //     
        //     let submitToStash = submitToStash logger client
        //     
        //     let batch = Cmd.batch [
        //         Cmd.ofMsg (UpdateUploadedFile file)
        //         Cmd.OfAsync.either submitToStash file Stashed Error
        //     ]
        //     
        //     { model with IsBusy = true }, batch
        //
        // | Stashed (file, deviation) ->
        //         
        //     let model = {
        //         model with
        //             StashedDeviations = [model.StashedDeviations; [|deviation|]] |> Array.concat
        //     }
        //     
        //     let batch = Cmd.batch [|
        //         Cmd.ofMsg (DeleteLocalFile file)
        //         Cmd.ofMsg (SaveStashedFile deviation)
        //     |]
        //     
        //     model, batch
