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

    [<EntryPoint>]
    let main _ =
        // File.Delete ".persistence"
        
        let secrets = Secrets.load ()
        let persistence = Persistence.File<AuthenticationData>()
        let client = Client(persistence, secrets.client_id, secrets.client_secret)
        let deviations = Data.readDeviations ()
        
        let profile = client.WhoAmI() |> AsyncResult.getOrFail |> Async.RunSynchronously
        
        printfn $"Hello, {profile.username}!"
        
        for deviation in deviations do
            printfn $"Processing '{JsonConvert.SerializeObject deviation}'..."
        
        printfn "Bye!"
        
        0
