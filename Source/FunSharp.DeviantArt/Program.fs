namespace FunSharp.DeviantArt

open System.IO
open Newtonsoft.Json
open FunSharp.Common
open FunSharp.DeviantArt
open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model

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
        // File.Delete ".persistence"
        
        let secrets = Secrets.load ()
        let persistence = Persistence.File<ApiResponses.Token>()
        let client = Client(persistence, secrets.client_id, secrets.client_secret)
        
        let profile = client.WhoAmI() |> Async.RunSynchronously
        
        printfn $"Hello, {profile.username}!"
        
        // printfn $"{persistence.ToString()}"

        // getAllDeviationsWithMetadata client
        
        let filePath1 = "C:\\Users\\User\\Documents\\Sora\\images\\spicy\\20250822_1917_Mystical Fire Goddess_simple_compose_01k39cfzjxeqmraztn2etdw422.png"
        let filePath2 = "C:\\Users\\User\\Documents\\Sora\\images\\spicy\\20250821_0951_Enchanting Forest Command_simple_compose_01k35sp2h8e74bzhsejh05vq80.png"
        
        let oneFile : Http.File array = [|
            { Title = "a1"; Content = File.ReadAllBytes filePath1; MediaType = Some "image/png" }
        |]
        
        // client.SubmitToStash("test", [|"a", filePath|])
        // client.SubmitToStash("test", twoFiles)
        // client.SubmitToStash(filePath, 3307645702784122L)
        // client.SubmitToStash(filePath, "Sta.sh")
        // client.SubmitToStash(filePath, "Test")
        // client.SubmitToStash("test_title", [|"a", filePath; "b", filePath|], "test_folder")
        // client.ReplaceInStash(filePath, 4885876966633365L)
        // client.ReplaceInStash("test2", oneFile, 865778078484153L)
        client.SubmitToStash("test abc", oneFile)
        |> Async.tee (fun submission -> printfn $"{submission}")
        |> Async.Ignore
        |> Async.RunSynchronously
        
        // printfn $"{client.Test() |> Async.RunSynchronously}"

        printfn "Bye!"
        
        0
