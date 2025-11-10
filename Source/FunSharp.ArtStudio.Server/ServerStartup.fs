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
    
    let mutable backgroundWorkerState = false
    
    let randomDelay_Fast = (
        TimeSpan.FromSeconds(10).TotalMilliseconds |> int,
        TimeSpan.FromSeconds(10).TotalMilliseconds |> int
    )
    
    let randomDelay_Medium = (
        TimeSpan.FromSeconds(11).TotalMilliseconds |> int,
        TimeSpan.FromSeconds(34).TotalMilliseconds |> int
    )
    
    let randomDelay_Slow = (
        TimeSpan.FromSeconds(31).TotalMilliseconds |> int,
        TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(14)).TotalMilliseconds |> int
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
            GET >=> path $"{apiBase}/local/tasks/status" >=> getBackgroundTasksStatus ()
            
            GET >=> path $"{apiBase}/local/inspirations" >=> getInspirations persistence
            GET >=> path $"{apiBase}/local/prompts" >=> getPrompts persistence
            GET >=> path $"{apiBase}/local/tasks" >=> getTasks persistence
            GET >=> path $"{apiBase}/local/gpt-results" >=> getChatGPTResults persistence
            GET >=> path $"{apiBase}/local/sora-results" >=> getSoraResults persistence
            GET >=> path $"{apiBase}/local/deviations" >=> getLocalDeviations persistence
            GET >=> path $"{apiBase}/stash" >=> getStashedDeviations persistence
            GET >=> path $"{apiBase}/publish" >=> getPublishedDeviations persistence
            
            PUT >=> path $"{apiBase}/local/images" >=> putImages serverAddress serverPort
            PUT >=> path $"{apiBase}/local/inspiration" >=> putInspiration persistence

            PUT >=> path $"{apiBase}/local/prompt" >=> addPrompt persistence
            PUT >=> path $"{apiBase}/local/deviation" >=> (fun ctx -> badRequestMessage ctx "addDeviation()" "not implemented yet")
            
            POST >=> path $"{apiBase}/stash" >=> stash persistence deviantArtClient
            POST >=> path $"{apiBase}/publish" >=> publish persistence deviantArtClient secrets
            
            // TODO: can client just do all the calls for any of these?
            POST >=> path $"{apiBase}/inspiration2prompt" >=> inspiration2Prompt persistence
            POST >=> path $"{apiBase}/inspiration2gpt" >=> inspiration2ChatGPTTask persistence
            POST >=> path $"{apiBase}/prompt2deviation" >=> prompt2Deviation persistence
            POST >=> path $"{apiBase}/prompt2sora" >=> prompt2SoraTask persistence
            POST >=> path $"{apiBase}/retry-sora" >=> retrySora persistence
            POST >=> path $"{apiBase}/sora2deviation" >=> sora2Deviation persistence
            
            PATCH >=> path $"{apiBase}/local/prompt" >=> patchPrompt persistence
            PATCH >=> path $"{apiBase}/local/deviation" >=> patchLocalDeviation persistence
            
            DELETE >=> path $"{apiBase}/local/inspiration" >=> deleteInspiration persistence
            DELETE >=> path $"{apiBase}/local/gpt-result" >=> deleteChatGPTResult persistence
            DELETE >=> path $"{apiBase}/local/prompt" >=> deletePrompt persistence
            DELETE >=> path $"{apiBase}/local/sora-result" >=> deleteSoraResult persistence
            DELETE >=> path $"{apiBase}/local/deviation" >=> deleteLocalDeviation persistence
            DELETE >=> path $"{apiBase}/stash" >=> deleteStashedDeviation persistence
            
            pathScan "/images/%s" (fun filename ->
                let filepath = Path.Combine(imagesLocation, filename)
                file filepath
            )
            
            pathScan "/automated/%s" (fun filename ->
                let filepath = Path.Combine(automatedImagesLocation, filename)
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
            
    let startBackgroundWorker label (cts: CancellationTokenSource) delay workerAction =
        
        printfn $"Starting background worker ({label})..."
        
        let backgroundJob = BackgroundWorker(cts.Token, delay, workerAction)
        backgroundJob.Work () |> ignore
        
    let startBackgroundWorkers cts persistence deviantArtClient soraClient =
        
        fun () -> BackgroundTasks.Inspiration.processTasks serverAddress serverPort persistence deviantArtClient
        |> startBackgroundWorker "Inspiration" cts randomDelay_Fast
        
        fun () ->
            match backgroundWorkerState with
            | false ->
                BackgroundTasks.ChatGPT.processTasks persistence soraClient |> Async.RunSynchronously
            | true ->
                BackgroundTasks.Sora.processTasks persistence soraClient |> Async.RunSynchronously
                
            backgroundWorkerState <- not backgroundWorkerState
            Async.returnM ()
        |> startBackgroundWorker "ChatGPT + Sora" cts randomDelay_Medium
                
    [<EntryPoint>]
    let main _ =
        
        let persistence = new NewLiteDbPersistence(@"C:\Files\FunSharp.DeviantArt\persistence.db") :> IPersistence
        let deviantArtClient = new FunSharp.DeviantArt.Api.Client(persistence, secrets.client_id, secrets.client_secret)
        let soraClient = new FunSharp.OpenAI.Api.Sora.Client()
        
        // deviantArtClient.StartInteractiveLogin() |> Async.RunSynchronously
        if deviantArtClient.NeedsInteraction then
            deviantArtClient.StartInteractiveLogin() |> Async.RunSynchronously
            
        Async.Start(async { do startBackgroundWorkers cts persistence deviantArtClient soraClient }, cancellationToken = cts.Token)
        Async.Start(async { do tryStartServer persistence deviantArtClient }, cancellationToken = cts.Token)
        
        printfn "Press Enter to stop..."
        Console.ReadLine() |> ignore

        printfn "Shutting down..."
        cts.Cancel()
        
        persistence.Dispose()
        (deviantArtClient :> IDisposable).Dispose()
        (soraClient :> IDisposable).Dispose()
        
        0
