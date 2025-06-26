namespace DeviantArt.Api.Client

open System
open System.IO

open DeviantArt.Api

module Program =

    [<EntryPoint>]
    let main _ =
        let secrets = Secrets.load ()
        let persistence = Persistence.FilePersistence<DeviantArt.TokenResponse>()
        let client = DeviantArt.Client(persistence, secrets.client_id, secrets.client_secret)
        
        let profile = client.WhoAmI() |> Async.RunSynchronously
        
        printfn $"Hello, {profile.username}!"

        client.AllDeviationsWithMetadata()
        |> Async.RunSynchronously
        |> List.map (fun (deviation, metadata) ->
            match metadata.stats with
            | None -> String.Empty
            | Some stats ->
                [
                    $"{deviation.title} ({deviation.id})"
                    $"Views: {stats.views_today}/{stats.views}"
                    $"Favs: {stats.favourites}"
                    $"Comments: {stats.comments}"
                    $"Downloads: {stats.downloads_today}/{stats.downloads}"
                ]
                |> fun x -> String.Join (" ### ", x)
                
        )
        |> fun lines -> File.WriteAllLines("deviations_stats.txt", lines)

        printfn "Bye!"
        
        0
