namespace FunSharp.DeviantArt.Manager

open System
open System.IO
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

    type DbItem = {
        Id: string
        Value: byte array
    }
    
    let private pickler = FsPickler.CreateBinarySerializer()
    
    let private modelWithUpdatedUploadedFile model key (update: UploadedFile -> UploadedFile) = {
        model with
            UploadedFiles = model.UploadedFiles
            |> Array.map (fun x ->
                if x.FileName = key then
                    update x
                else
                    x
            )
    }
    
    let private processUpload (file: IBrowserFile) = async {
        let maxSize = 1024L * 1024L * 100L // 100 MB
        
        use stream = file.OpenReadStream(maxAllowedSize = maxSize)
        use ms = new MemoryStream()
        
        do! stream.CopyToAsync(ms)
        
        let byteArray = ms.ToArray()
        
        return (file.Name, $"data:{file.ContentType};base64,{Convert.ToBase64String(byteArray)}", byteArray)
    }
    
    let private loadDeviations<'T> (logger: ILogger) (database: IndexedDb) storeName=
        
        logger.LogTrace $"loading deviations from {storeName}..."
        
        database.Init(Common.dbName, [|Common.dbKeyStashedDeviations; Common.dbKeyPublishedDeviations|])
        |> Async.bind (fun () ->
            database.GetAll<DbItem>(storeName)
            |> Async.map (fun x -> x |> Array.map (fun x -> pickler.UnPickle<'T> x.Value))
        )
    
    let private loadStashedDeviations logger database =
        
        loadDeviations<StashedDeviation> logger database Common.dbKeyStashedDeviations
    
    let private loadPublishedDeviations logger database =
        
        loadDeviations<PublishedDeviation> logger database Common.dbKeyPublishedDeviations
        
    let private saveDeviation<'T> (logger: ILogger) (database: IndexedDb) storeName key (value: 'T) =
        
        logger.LogTrace $"saving deviation to '{storeName}': {value |> JsonConvert.SerializeObject}"
        
        let dbItem = {
            Id = key
            Value = pickler.Pickle value
        }
        
        database.Set(storeName, key, dbItem)
        
    let private saveStashedDeviation logger database key (value: StashedDeviation) =
        
        saveDeviation logger database Common.dbKeyStashedDeviations key value
        
    let private savePublishedDeviations logger database key (value: PublishedDeviation) =
        
        saveDeviation logger database Common.dbKeyPublishedDeviations key value
        
    let private submitToStash (logger: ILogger) (client: Client) file =
        
        logger.LogTrace $"Stashing {file.FileName}..."
        logger.LogTrace $"{file.Metadata |> JsonConvert.SerializeObject}"
        
        let submission = {
            StashSubmission.empty with
                Title = file.Metadata.Title
        }
        
        let httpFile : Http.File = {
            MediaType = Some "image/png"
            Content = file.Content
        }
        
        (submission, httpFile)
        |> client.SubmitToStash
        |> AsyncResult.getOrFail
        |> Async.map (fun response ->
            match response.status with
            | "success" ->
                {
                    StashId = response.item_id
                    Metadata = file.Metadata
                }
            | _ ->
                failwith $"Failed to stash {file.FileName}"
            |> fun x -> (file, x)
        )
    
    let update (logger: ILogger) (database: IndexedDb) (client: ApiClient) message (model: Model.State) =
    
        match message with
        | SetPage page ->
            { model with Page = page }, Cmd.none

        | Error ex ->
            { model with Error = Some ex.Message }, Cmd.none
            
        | ClearError ->
            { model with Error = None }, Cmd.none
            
        | UploadImages newFiles ->
            
            let alreadyExists (file: IBrowserFile) =
                model.UploadedFiles |> Array.exists (fun x -> x.FileName = file.Name)
                
            let newFiles =
                newFiles
                |> Array.filter (fun x -> alreadyExists x |> not)
            
            let processUploadCommands =
                newFiles
                |> Array.map (fun x -> Cmd.OfAsync.perform processUpload x FinishUpload)
                
            let newFiles =
                newFiles
                |> Array.map (fun x -> { UploadedFile.empty with FileName = x.Name })
                
            let consolidatedFiles =
                [model.UploadedFiles; newFiles] |> Array.concat
            
            { model with UploadedFiles = consolidatedFiles }, Cmd.batch processUploadCommands

        | FinishUpload (fileName, previewUrl, content) ->
            
            let update x =
                { x with PreviewUrl = previewUrl; Content = content }
            
            modelWithUpdatedUploadedFile model fileName update, Cmd.none

        | UpdateUploadedFile file ->
            
            let model = modelWithUpdatedUploadedFile model file.FileName (fun _ -> file)
            
            model, Cmd.none

        | LoadDeviations ->
            
            let loadAllDeviations () =
                loadStashedDeviations logger database
                |> Async.bind (fun stashed ->
                    loadPublishedDeviations logger database
                    |> Async.map (fun published ->
                        (stashed, published)
                    )
                ) 
            
            { model with IsBusy = true }, Cmd.OfAsync.perform loadAllDeviations () LoadedDeviations
            
        | LoadedDeviations (stashed, published) ->
                
            let model =
                {
                    model with
                        IsBusy = false
                        StashedDeviations = stashed
                        PublishedDeviations = published
                }
            
            logger.LogInformation $"Loaded {stashed.Length} stashed and {published.Length} published deviations."
            
            model, Cmd.none

        | SaveDeviation (file, deviationData) ->
            
            let cmd =
                match deviationData with
                | DeviationData.Stashed deviation ->
                    let func = saveStashedDeviation logger database file.FileName
                    Cmd.OfAsync.either func deviation (fun () -> SavedDeviation) Error
                | DeviationData.Published deviation ->
                    let func = savePublishedDeviations logger database file.FileName
                    Cmd.OfAsync.either func deviation (fun () -> SavedDeviation) Error
                
            { model with IsBusy = true }, cmd
            
        | SavedDeviation ->
            
            { model with IsBusy = false }, Cmd.none

        | Stash file ->
            
            let client =
                match model.Client with
                | None -> failwith $"cannot stash without a client"
                | Some c -> c
            
            let gallery = file.Metadata.Gallery
            
            let isMature =
                match Helpers.gallery gallery with
                | Helpers.Gallery.Spicy -> true
                | _ -> false
            
            let file = {
                file with
                    Metadata.Gallery = Helpers.galleryId gallery
                    Metadata.IsMature = isMature
            }
            
            let submitToStash = submitToStash logger client
            
            let batch = Cmd.batch [
                Cmd.ofMsg (UpdateUploadedFile file)
                Cmd.OfAsync.either submitToStash file Stashed Error
            ]
            
            { model with IsBusy = true }, batch

        | Stashed (file, deviation) ->
                
            let model = {
                model with
                    StashedDeviations = [model.StashedDeviations; [|deviation|]] |> Array.concat
            }
            
            model, Cmd.ofMsg (Message.SaveDeviation (file, DeviationData.Stashed deviation))

        | UpdateAuthData authData ->
            { model with AuthData = authData }, Cmd.none
            
        | SetupClient ->
            
            client.UpdateAuth(model.AuthData.AccessToken)
            
            model, Cmd.ofMsg (Message.SetPage Page.Home)
            
        | Test ->
            
            let test () =
                client.WhoAmI()
                |> AsyncResult.map(fun x ->
                    logger.LogTrace $"whoami: {x.username}"
                )
            
            model, Cmd.OfAsync.either test () (fun _ -> TestSucceeded) TestFailed
            
        | TestSucceeded ->
            
            logger.LogTrace "successful test"
            
            model, Cmd.none
            
        | TestFailed ex ->
            
            logger.LogError $"test failed: {ex.Message}"
            
            model, Cmd.none
