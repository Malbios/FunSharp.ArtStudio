namespace FunSharp.ArtStudio.Server

open System
open System.Threading
open FunSharp.ArtStudio.Server.Helpers
open FunSharp.ArtStudio.Server.Routing
open Suave
open FunSharp.Common
open FunSharp.Data
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.Api.Model

module ServerStartup =
    
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
    
    let cts = new CancellationTokenSource()
    let secrets = Secrets.load ()
    let mutable backgroundWorkerState = false
    
    let serverConfiguration = {
        defaultConfig with
            cancellationToken = cts.Token
            bindings = [ HttpBinding.createSimple HTTP serverAddress serverPort ] 
    }
        
    let tryStartServer persistence (deviantArtClient: FunSharp.DeviantArt.Api.Client) soraClient=
        
        try
            let who = deviantArtClient.WhoAmI() |> Async.getOrFail |> Async.RunSynchronously
            printfn $"Hello, {who.username}!"
            
            startWebServer serverConfiguration (routing secrets persistence deviantArtClient soraClient)
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
        Async.Start(async { do tryStartServer persistence deviantArtClient soraClient }, cancellationToken = cts.Token)
        
        printfn "Press Enter to stop..."
        Console.ReadLine() |> ignore

        printfn "Shutting down..."
        cts.Cancel()
        
        persistence.Dispose()
        (deviantArtClient :> IDisposable).Dispose()
        (soraClient :> IDisposable).Dispose()
        
        0
