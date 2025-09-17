namespace FunSharp.DeviantArt.Server

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
open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Server.Helpers
open FunSharp.DeviantArt.Server.WebParts

module ServerStartup =
    
    let serverAddress = "127.0.0.1"
    let serverPort = 5123
    let apiBase = "/api/v1"
    
    let secrets = Secrets.load ()
    let authPersistence = Persistence.AuthenticationPersistence()
    let dataPersistence = new PickledPersistence(@"C:\Files\FunSharp.DeviantArt\persistence.db") :> IPersistence
    let apiClient = Client(authPersistence, secrets.client_id, secrets.client_secret)
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
            
            GET >=> path $"{apiBase}/local/inspirations" >=> getInspirations dataPersistence
            GET >=> path $"{apiBase}/local/prompts" >=> getPrompts dataPersistence
            GET >=> path $"{apiBase}/local/deviations" >=> getLocalDeviations dataPersistence
            GET >=> path $"{apiBase}/stash" >=> getStashedDeviations dataPersistence
            GET >=> path $"{apiBase}/publish" >=> getPublishedDeviations dataPersistence
            
            POST >=> path $"{apiBase}/local/images" >=> uploadImages serverAddress serverPort
            POST >=> path $"{apiBase}/local/inspiration" >=> addInspiration serverAddress serverPort dataPersistence apiClient
            POST >=> path $"{apiBase}/local/prompt" >=> (fun ctx -> badRequestMessage ctx "addPrompt()" "not implemented yet")
            POST >=> path $"{apiBase}/local/deviation" >=> (fun ctx -> badRequestMessage ctx "addDeviation()" "not implemented yet")
            POST >=> path $"{apiBase}/stash" >=> stash dataPersistence apiClient
            POST >=> path $"{apiBase}/publish" >=> publish secrets dataPersistence apiClient
            
            POST >=> path $"{apiBase}/inspiration2prompt" >=> inspiration2Prompt dataPersistence
            POST >=> path $"{apiBase}/prompt2deviation" >=> prompt2Deviation dataPersistence
            
            POST >=> path $"{apiBase}/local/deviation/asImages" >=> uploadLocalDeviations serverAddress serverPort dataPersistence
            
            PATCH >=> path $"{apiBase}/local/deviation" >=> updateLocalDeviation dataPersistence
            
            DELETE >=> path $"{apiBase}/local/inspiration" >=> forgetInspiration dataPersistence
            DELETE >=> path $"{apiBase}/local/prompt" >=> forgetPrompt dataPersistence
            DELETE >=> path $"{apiBase}/local/deviation" >=> forgetLocalDeviation dataPersistence
            
            pathScan "/images/%s" (fun filename ->
                let filepath = Path.Combine(imagesLocation, filename)
                file filepath
            )
            
            NOT_FOUND "unknown path"
        ]
        
    let tryStartServer () =
        
        try
            let who = apiClient.WhoAmI() |> AsyncResult.getOrFail |> Async.RunSynchronously
            printfn $"Hello, {who.username}!"
            
            startWebServer serverConfiguration routing
        with
        | :? System.Net.Sockets.SocketException as ex ->
            printfn $"Socket bind failed: %s{ex.Message}"
            
    [<EntryPoint>]
    let main _ =
        
        Async.Start(async { do tryStartServer () }, cancellationToken = cts.Token)
        
        printfn "Press Enter to stop..."
        Console.ReadLine() |> ignore

        printfn "Shutting down..."
        
        cts.Cancel()
        dataPersistence.Dispose()
        
        0
