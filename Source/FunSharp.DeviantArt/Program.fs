namespace FunSharp.DeviantArt

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
        
    let galleryId gallery = galleries[gallery]
    
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
        
    let withGalleryId (deviation: Data.LocalDeviation) =
        let gallery =
            match deviation.Metadata.Gallery with
            | v when v = "RandomPile" -> Gallery.RandomPile
            | v when v = "Spicy" -> Gallery.Spicy
            | v when v = "Scenery" -> Gallery.Scenery
            | v -> failwith $"unexpected gallery: {v}"
            
        { deviation with Metadata.Gallery = galleryId gallery }
        
    let stashNewDeviations (client: Client) =
        
        let deviations = Data.readLocalDeviations ()
        
        for deviation in deviations do
            let deviation = deviation |> withGalleryId
            
            let submission = {
                StashSubmission.defaults with
                    Title = deviation.Metadata.Title
            }
            
            let file : Http.File = {
                MediaType = Some "image/png"
                Content = File.ReadAllBytes deviation.FilePath
            }
            
            client.SubmitToStash(submission, file)
            |> getOrFail
            |> fun response ->
                printfn $"Status: {response.status}"
                printfn $"ID: {response.item_id}"
                printfn $"Inspired by {deviation.Metadata.Inspiration}"
                printfn ""
        
    let publishNewDeviations (client: Client) =
        
        let deviations = Data.readLocalDeviations ()
        
        for deviation in deviations do
            let deviation = deviation |> withGalleryId
            
            let submission = {
                StashSubmission.defaults with
                    Title = deviation.Metadata.Title
            }
            
            let file : Http.File = {
                MediaType = Some "image/png"
                Content = File.ReadAllBytes deviation.FilePath
            }
            
            client.SubmitToStash(submission, file)
            |> AsyncResult.bind (fun response ->
                {
                    StashPublication.defaults with
                        ItemId = response.item_id
                        IsMature = deviation.Metadata.IsMature
                        Galleries = [deviation.Metadata.Gallery] |> Array.ofList
                }
                |> client.PublishFromStash
            )
            |> getOrFail
            |> fun response ->
                printfn $"Status: {response.status}"
                printfn $"URL: {response.url}"
                printfn $"Inspired by {deviation.Metadata.Inspiration}"
                printfn ""
                
    let importExistingDeviations (dataPersistence: Persistence.LiteDb<Data.Deviation>) (client: Client) =
        
        let existingDeviations = dataPersistence.Load()
        
        let localDeviations =
            existingDeviations
            |> Array.map (fun x ->
                match x with
                | Data.Local x -> x
                | _ -> ()
            )
            
        let stashedDeviations =
            existingDeviations
            |> Array.map (fun x ->
                match x with
                | Data.Stashed x -> x
                | _ -> ()
            )
            
        let publishedDeviations =
            existingDeviations
            |> Array.map (fun x ->
                match x with
                | Data.Published x -> x
                | _ -> ()
            )
            
        let deviantArtDeviations = client.AllDeviations() |> getOrFail
        
        let newDeviantArtDeviations =
            deviantArtDeviations
            |> Array.filter (fun deviation ->
                publishedDeviations |> Array.forall (fun known -> )
            )

    [<EntryPoint>]
    let main args =
        // File.Delete ".persistence"
        
        let secrets = Secrets.load ()
        let authPersistence = Persistence.File<AuthenticationData>()
        let dataPersistence = Persistence.LiteDb<Data.Deviation>("persistence.db", "deviations")
        let client = Client(authPersistence, secrets.client_id, secrets.client_secret)
        
        let profile = client.WhoAmI() |> AsyncResult.getOrFail |> Async.RunSynchronously
        if profile.username = "" then
            failwith "Something went wrong! Could not read profile username."
        
        printfn $"Hello, {profile.username}!"
        printfn ""
        
        let cmd = args |> Array.tryHead |> Option.defaultValue ""
        match cmd with
        | "" -> ()
        | "import" -> importExistingDeviations client
        | "stash" -> stashNewDeviations client
        | "publish" -> publishNewDeviations client
        | _ -> printfn $"invalid command: {cmd}"
        
        printfn "Bye!"
        
        0
