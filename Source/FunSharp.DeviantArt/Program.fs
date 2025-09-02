namespace FunSharp.DeviantArt

open System
open System.IO
open System.Reflection
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
        
    let resetLocalDeviations () =
    
        let asm = Assembly.GetExecutingAssembly()
        use stream = asm.GetManifestResourceStream("DeviantArtApiClient.data.json")
        if isNull stream then
            let names = String.Join(", ", asm.GetManifestResourceNames())
            printfn $"{names}"
            failwithf "Resource '%s' not found" "data.json"
        use reader = new StreamReader(stream)
        reader.ReadToEnd()
        |> fun x -> File.WriteAllText("data.json", x)
        
    let readLocalDeviations () =
        
        if not (File.Exists "data.json") then
            resetLocalDeviations ()
        
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
        
    let inspirationAlreadyPublished (existingDeviations: DeviationData array) inspiration =
        
        existingDeviations |> Array.exists (fun x ->
            match x with
            | Published x -> x.Metadata.Inspiration = inspiration
            | _ -> false
        )
        
    let stashNewDeviations (persistence: PickledPersistence<DeviationData, string>) (client: Client) =
        
        let deviations = readLocalDeviations ()
        let existingDeviations = persistence.FindAll()
        
        for deviation in deviations do
            if not (File.Exists deviation.FilePath) then
                failwith $"[Deviation Metadata] File does not exist: {deviation.FilePath}"
            
            if deviation.Title = "" then
                failwith "[Deviation Metadata] Title is empty!"
                
            if deviation.Inspiration |> inspirationAlreadyPublished existingDeviations then
                failwith $"Inspiration already published: {deviation.Inspiration}"
                
            galleryId deviation.Gallery |> ignore
            
            let submission = {
                StashSubmission.defaults with
                    Title = deviation.Title
            }
            
            let file : Http.File = {
                MediaType = Some "image/png"
                Content = File.ReadAllBytes deviation.FilePath
            }
            
            client.SubmitToStash(submission, file)
            |> getOrFail
            |> fun response ->
                match response.status with
                | "success" ->
                    DeviationData.Stashed {
                        StashId = response.item_id
                        Metadata = deviation
                    }
                    |> fun x -> (deviation.FilePath, x)
                    |> persistence.Upsert
                    |> fun x ->
                        printfn $"success: {x}"
                        printfn $"URL: {stashUrl response.item_id}"
                        
                        if deviation.Inspiration <> null then
                            printfn $"Inspired by {deviation.Inspiration}"
                        
                        printfn ""
                    
                | _ ->
                    printfn $"Failed to stash {deviation.FilePath}"
                    
        resetLocalDeviations ()
        
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
