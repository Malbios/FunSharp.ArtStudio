namespace FunSharp.DeviantArt

open System.IO
open FunSharp.DeviantArt
open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model
open Newtonsoft.Json

module Program =
    
    let getAllDeviationsWithMetadata (client: Client) =
        client.AllDeviationsWithMetadata()
        |> Async.RunSynchronously
        |> List.map (fun (deviation, metadata) ->
            match metadata.stats with
            | None -> { id = deviation.id; title = deviation.title; description = metadata.description; stats = Stats.empty }
            | Some stats -> { id = deviation.id; title = deviation.title; description = metadata.description; stats = stats }
        )
        |> List.sortBy (fun x -> x.stats.views, x.stats.favourites, x.stats.comments)
        |> fun x -> File.WriteAllText ("deviations.json", JsonConvert.SerializeObject(x))

    [<EntryPoint>]
    let main _ =
        let secrets = Secrets.load ()
        let persistence = Persistence.File<ApiResponses.Token>()
        let client = Client(persistence, secrets.client_id, secrets.client_secret)
        
        let profile = client.WhoAmI() |> Async.RunSynchronously
        
        printfn $"Hello, {profile.username}!"
        
        // printfn $"{persistence.ToString()}"

        // getAllDeviationsWithMetadata client
            
        {
            FilePath = "C:\\Users\\User\\Documents\\Sora\\images\\spicy\\20250822_1917_Mystical Fire Goddess_simple_compose_01k39cfzjxeqmraztn2etdw422.png"
            Title = "Test Title"
        }
        |> client.SubmitToStash
        |> Async.map (fun submission ->
            printfn $"{submission}"
        )
        |> Async.RunSynchronously

        printfn "Bye!"
        
        0
