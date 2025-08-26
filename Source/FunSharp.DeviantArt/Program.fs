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
        if Debug.isEnabled then
            File.Delete ".persistence"
        
        let secrets = Secrets.load ()
        let persistence = Persistence.File<ApiResponses.Token>()
        let client = Client(persistence, secrets.client_id, secrets.client_secret)
        
        let profile = client.WhoAmI() |> Async.RunSynchronously
        
        printfn $"Hello, {profile.username}!"
        
        // printfn $"{persistence.ToString()}"

        // getAllDeviationsWithMetadata client
        
        let filePath1 = "C:\\Users\\User\\Documents\\Sora\\images\\spicy\\20250822_1917_Mystical Fire Goddess_simple_compose_01k39cfzjxeqmraztn2etdw422.png"
        let filePath2 = "C:\\Users\\User\\Documents\\Sora\\images\\spicy\\20250821_0951_Enchanting Forest Command_simple_compose_01k35sp2h8e74bzhsejh05vq80.png"
        
        let oneFile : Http.File = { Title = "a1"; Content = File.ReadAllBytes filePath1; MediaType = Some "image/png" }
        
        let itemId = 2252502243219020L
        
        let stashPublication = {
            IsMature = true
            // MatureLevel = MatureLevel.Moderate
            // MatureClassification = [| MatureClassification.Sexual |]
            Feature = false
            AllowComments = true
            // DisplayResolution = DisplayResolution.Original
            LicenseOptions = { CreativeCommons = true; Commercial = true; Modify = LicenseOptionsModify.Share }
            // Galleries = [| Gallery.Spicy |]
            AllowFreeDownload = true
            AddWatermark = false
            Tags = Array.empty
            // Groups = Array.empty
            // GroupFolders = Array.empty
            IsAiGenerated = true
            NoAi = false
            ItemId = itemId
        }
        // client.SubmitToStash("test abc", oneFile)
        stashPublication
        |> client.PublishFromStash 
        |> Async.tee (fun submission -> printfn $"{submission}")
        |> Async.Ignore
        |> Async.RunSynchronously

        printfn "Bye!"
        
        0
