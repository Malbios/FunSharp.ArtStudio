namespace FunSharp.ArtStudio.Server

open System
open System.IO
open System.Threading
open Suave
open Suave.Files
open Suave.Filters
open Suave.Operators
open Suave.RequestErrors
open FunSharp.Common
open FunSharp.Data
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.Api.Model
open FunSharp.ArtStudio.Server.Helpers
open FunSharp.ArtStudio.Server.WebParts

module ServerStartup =
    
    let serverAddress = "127.0.0.1"
    let serverPort = 5123
    let apiBase = "/api/v1"
    
    let randomDelay_Test = (
        TimeSpan.FromSeconds(3).TotalMilliseconds |> int,
        TimeSpan.FromSeconds(9).TotalMilliseconds |> int
    )
    
    let randomDelay_Prod = (
        TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(23)).TotalMilliseconds |> int,
        TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(48)).TotalMilliseconds |> int
    )
    
    let secrets = Secrets.load ()
    let cts = new CancellationTokenSource()
    
    let serverConfiguration = {
        defaultConfig with
            cancellationToken = cts.Token
            bindings = [ HttpBinding.createSimple HTTP serverAddress serverPort ] 
    }
    
    let routing persistence deviantArtClient =
            
        allowCors >=> choose [
            corsPreflight
            
            GET >=> path $"{apiBase}/user/name" >=> getUsername deviantArtClient
            GET >=> path $"{apiBase}/settings" >=> getSettings secrets
            
            GET >=> path $"{apiBase}/local/inspirations" >=> getInspirations persistence
            GET >=> path $"{apiBase}/local/prompts" >=> getPrompts persistence
            GET >=> path $"{apiBase}/local/deviations" >=> getLocalDeviations persistence
            GET >=> path $"{apiBase}/stash" >=> getStashedDeviations persistence
            GET >=> path $"{apiBase}/publish" >=> getPublishedDeviations persistence
            
            PUT >=> path $"{apiBase}/local/images" >=> putImages serverAddress serverPort
            PUT >=> path $"{apiBase}/local/inspiration" >=> putInspiration serverAddress serverPort persistence deviantArtClient

            PUT >=> path $"{apiBase}/local/prompt" >=> (fun ctx -> badRequestMessage ctx "addPrompt()" "not implemented yet")
            PUT >=> path $"{apiBase}/local/deviation" >=> (fun ctx -> badRequestMessage ctx "addDeviation()" "not implemented yet")
            
            POST >=> path $"{apiBase}/stash" >=> stash persistence deviantArtClient
            POST >=> path $"{apiBase}/publish" >=> publish persistence deviantArtClient secrets
            
            POST >=> path $"{apiBase}/inspiration2prompt" >=> inspiration2Prompt persistence
            POST >=> path $"{apiBase}/prompt2deviation" >=> prompt2Deviation persistence
            
            PATCH >=> path $"{apiBase}/local/prompt" >=> patchPrompt persistence
            PATCH >=> path $"{apiBase}/local/deviation" >=> patchLocalDeviation persistence
            
            DELETE >=> path $"{apiBase}/local/inspiration" >=> deleteInspiration persistence
            DELETE >=> path $"{apiBase}/local/prompt" >=> deletePrompt persistence
            DELETE >=> path $"{apiBase}/local/deviation" >=> deleteLocalDeviation persistence
            DELETE >=> path $"{apiBase}/stash" >=> deleteStashedDeviation persistence
            
            pathScan "/images/%s" (fun filename ->
                let filepath = Path.Combine(imagesLocation, filename)
                file filepath
            )
            
            NOT_FOUND "unknown path"
        ]
        
    let tryStartServer persistence (deviantArtClient: FunSharp.DeviantArt.Api.Client) =
        
        try
            let who = deviantArtClient.WhoAmI() |> Async.getOrFail |> Async.RunSynchronously
            printfn $"Hello, {who.username}!"
            
            startWebServer serverConfiguration (routing persistence deviantArtClient)
        with
        | :? System.Net.Sockets.SocketException as ex ->
            printfn $"Socket bind failed: %s{ex.Message}"
        
    let startBackgroundWorker (cts: CancellationTokenSource) task =
        
        printfn "Starting background worker..."
        
        let backgroundJob = BackgroundWorker(cts.Token, randomDelay_Test, task)
        backgroundJob.Work () |> ignore
            
    let processPendingPrompts () =
        
        Async.returnM ()
        
    [<EntryPoint>]
    let main _ =
        
        use persistence = new NewLiteDbPersistence(@"C:\Files\FunSharp.DeviantArt\persistence.db") :> IPersistence
        use deviantArtClient = new FunSharp.DeviantArt.Api.Client(persistence, secrets.client_id, secrets.client_secret)
        use soraClient = new FunSharp.OpenAI.Api.Sora.Client()
        
        if deviantArtClient.NeedsInteraction then
            deviantArtClient.StartInteractiveLogin() |> Async.RunSynchronously
            
        startBackgroundWorker cts processPendingPrompts
        
        Async.Start(async { do tryStartServer persistence deviantArtClient }, cancellationToken = cts.Token)
        
        printfn "Press Enter to stop..."
        Console.ReadLine() |> ignore

        printfn "Shutting down..."
        cts.Cancel()
        
        0
