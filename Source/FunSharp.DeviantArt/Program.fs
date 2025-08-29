namespace FunSharp.DeviantArt

open System
open System.IO
open Newtonsoft.Json
open FunSharp.Common
open FunSharp.DeviantArt
open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model

module Program =
    
    type Gallery =
        | Featured
        | Caricatures
        | Spicy
        | Scenery
        | RandomPile
        
    let galleries =
        [
            Gallery.Featured, "487A4797-E595-CA89-7083-32FCD1F33831"
            Gallery.Caricatures, "01EFCC0B-6625-48F5-1C09-74B69FFCA526"
            Gallery.Spicy, "EAC6F867-87CA-333C-9C09-74C7587BAFAF"
            Gallery.Scenery, "B6120853-CD73-52D0-35D3-61BC719AE611"
            Gallery.RandomPile, "A5FA99E2-3756-B8A3-E145-59666660C224"
        ]
        |> Map.ofList
        
    let galleryId (galleryName: string) =
        let gallery =
            match galleryName with
            | v when v = "RandomPile" -> Gallery.RandomPile
            | v when v = "Spicy" -> Gallery.Spicy
            | v when v = "Scenery" -> Gallery.Scenery
            | v -> failwith $"unexpected gallery: {v}"
        
        galleries[gallery]
        
    let stashUrl itemId =
        $"https://sta.sh/0{Base36.encode itemId}"
        
    let readLocalDeviations () =
        
        File.ReadAllText "data.json"
        |> JsonConvert.DeserializeObject<DeviationMetadata array>
    
    let getOrFail callResult =
        
        callResult
        |> AsyncResult.getOrFail
        |> Async.RunSynchronously
    
    let getAllDeviationsWithMetadata (client: Client) =
        
        client.AllDeviationsWithMetadata()
        |> getOrFail
        |> List.map (fun (deviation, metadata) ->
            match metadata.stats with
            | None -> { id = deviation.id; title = deviation.title; description = metadata.description; stats = Stats.empty }
            | Some stats -> { id = deviation.id; title = deviation.title; description = metadata.description; stats = stats }
        )
        |> List.sortBy (fun x -> x.stats.views, x.stats.favourites, x.stats.comments)
        |> fun x -> File.WriteAllText ("deviations.json", JsonConvert.SerializeObject(x))
        
    let stashNewDeviations (persistence: PickledPersistence<DeviationData, string>) (client: Client) =
        
        let metadata = readLocalDeviations ()
        
        for metadata in metadata do
            if metadata.Inspiration = null then
                failwith "Deviation Metadata: inspiration is empty!"
            
            if metadata.Title = "" then
                failwith "Deviation Metadata: title is empty!"
            
            let submission = {
                StashSubmission.defaults with
                    Title = metadata.Title
            }
            
            let file : Http.File = {
                MediaType = Some "image/png"
                Content = File.ReadAllBytes metadata.FilePath
            }
            
            client.SubmitToStash(submission, file)
            |> getOrFail
            |> fun response ->
                match response.status with
                | "success" ->
                    DeviationData.Stashed {
                        StashId = response.item_id
                        Metadata = metadata
                    }
                    |> fun x -> (metadata.FilePath, x)
                    |> persistence.Insert
                    |> fun x ->
                        printfn $"success: {x}"
                        printfn $"URL: {stashUrl response.item_id}"
                        printfn $"Inspired by {metadata.Inspiration}"
                        printfn ""
                    
                | _ ->
                    printfn $"Failed to stash {metadata.FilePath}"
        
    let publishNewDeviations (persistence: PickledPersistence<DeviationData, string>) (client: Client) =
        
        let data =
            persistence.FindAll()
            |> Array.choose (fun x ->
                match x with
                | Stashed x -> Some x
                | _ -> None
            )
        
        for deviation in data do
            {
                StashPublication.defaults with
                    ItemId = deviation.StashId
                    IsMature = deviation.Metadata.IsMature
                    Galleries = [galleryId deviation.Metadata.Gallery] |> Array.ofList
            }
            |> client.PublishFromStash
            |> getOrFail
            |> fun response ->
                match response.status with
                | "success" ->
                    DeviationData.Published {
                        Url = Uri response.url
                        Metadata = deviation.Metadata
                    }
                    |> fun x -> (deviation.Metadata.FilePath, x)
                    |> persistence.Update
                    |> fun x ->
                        printfn $"success: {x}"
                        printfn $"URL: {response.url}"
                        printfn ""
                    
                | _ ->
                    printfn $"Failed to publish {deviation.Metadata.FilePath}"
                    
    [<EntryPoint>]
    let main args =
        // File.Delete "persistence.db"
        
        let secrets = Secrets.load ()
        let authPersistence = Persistence.AuthenticationPersistence()
        let dataPersistence = PickledPersistence<DeviationData, string>("persistence.db", "deviations")
        let client = Client(authPersistence, secrets.client_id, secrets.client_secret)
        
        let profile = client.WhoAmI() |> AsyncResult.getOrFail |> Async.RunSynchronously
        if profile.username = "" then
            failwith "Something went wrong! Could not read profile username."
        
        printfn $"Hello, {profile.username}!"
        printfn ""
        
        let cmd = args |> Array.tryHead |> Option.defaultValue ""
        match cmd with
        | "" -> ()
        | "stash" -> stashNewDeviations dataPersistence client
        | "publish" -> publishNewDeviations dataPersistence client
        | _ -> printfn $"invalid command: {cmd}"
        
        printfn "Bye!"
        
        0
