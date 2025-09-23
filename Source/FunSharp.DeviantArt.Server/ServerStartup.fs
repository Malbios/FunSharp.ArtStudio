namespace FunSharp.DeviantArt.Server

open System
open System.IO
open System.Net.Http
open System.Threading
open Suave
open Suave.Files
open Suave.Filters
open Suave.Operators
open Suave.RequestErrors
open FunSharp.Common
open FunSharp.Data
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Server.Helpers
open FunSharp.DeviantArt.Server.WebParts

module ServerStartup =
    
    let serverAddress = "127.0.0.1"
    let serverPort = 5123
    let apiBase = "/api/v1"
    
    let secrets = Secrets.load ()
    let httpClient = new HttpClient()
    let persistence = new NewLiteDbPersistence(@"C:\Files\FunSharp.DeviantArt\persistence.db") :> IPersistence
    let apiClient = new Client(persistence, httpClient, secrets.client_id, secrets.client_secret)
    let disposableApiClient = apiClient :> IDisposable
    let cts = new CancellationTokenSource()
    
    let serverConfiguration = {
        defaultConfig with
            cancellationToken = cts.Token
            bindings = [ HttpBinding.createSimple HTTP serverAddress serverPort ] 
    }
    
    let routing =
            
        allowCors >=> choose [
            corsPreflight
            
            GET >=> path $"{apiBase}/user/name" >=> username apiClient
            GET >=> path $"{apiBase}/settings" >=> getSettings secrets
            
            GET >=> path $"{apiBase}/local/inspirations" >=> getInspirations persistence
            GET >=> path $"{apiBase}/local/prompts" >=> getPrompts persistence
            GET >=> path $"{apiBase}/local/deviations" >=> getLocalDeviations persistence
            GET >=> path $"{apiBase}/stash" >=> getStashedDeviations persistence
            GET >=> path $"{apiBase}/publish" >=> getPublishedDeviations persistence
            
            POST >=> path $"{apiBase}/local/images" >=> uploadImages serverAddress serverPort
            POST >=> path $"{apiBase}/local/inspiration" >=> addInspiration serverAddress serverPort persistence apiClient
            POST >=> path $"{apiBase}/local/prompt" >=> (fun ctx -> badRequestMessage ctx "addPrompt()" "not implemented yet")
            POST >=> path $"{apiBase}/local/deviation" >=> (fun ctx -> badRequestMessage ctx "addDeviation()" "not implemented yet")
            POST >=> path $"{apiBase}/stash" >=> stash persistence apiClient
            POST >=> path $"{apiBase}/publish" >=> publish secrets persistence apiClient
            
            POST >=> path $"{apiBase}/inspiration2prompt" >=> inspiration2Prompt persistence
            POST >=> path $"{apiBase}/prompt2deviation" >=> prompt2Deviation persistence
            
            POST >=> path $"{apiBase}/local/deviation/asImages" >=> uploadLocalDeviations serverAddress serverPort persistence
            
            PATCH >=> path $"{apiBase}/local/deviation" >=> updateLocalDeviation persistence
            
            DELETE >=> path $"{apiBase}/local/inspiration" >=> forgetInspiration persistence
            DELETE >=> path $"{apiBase}/local/prompt" >=> forgetPrompt persistence
            DELETE >=> path $"{apiBase}/local/deviation" >=> forgetLocalDeviation persistence
            
            pathScan "/images/%s" (fun filename ->
                let filepath = Path.Combine(imagesLocation, filename)
                file filepath
            )
            
            NOT_FOUND "unknown path"
        ]
        
    let tryStartServer () =
        
        try
            let who = apiClient.WhoAmI() |> Async.getOrFail |> Async.RunSynchronously
            printfn $"Hello, {who.username}!"
            
            startWebServer serverConfiguration routing
        with
        | :? System.Net.Sockets.SocketException as ex ->
            printfn $"Socket bind failed: %s{ex.Message}"
            
    [<EntryPoint>]
    let main _ =
        
        if apiClient.NeedsInteraction then
            apiClient.StartInteractiveLogin() |> Async.RunSynchronously
        
        Async.Start(async { do tryStartServer () }, cancellationToken = cts.Token)
        
        printfn "Press Enter to stop..."
        Console.ReadLine() |> ignore

        printfn "Shutting down..."
        
        cts.Cancel()
        persistence.Dispose()
        disposableApiClient.Dispose()
        httpClient.Dispose()
        
        0
