namespace FunSharp.ArtStudio.Server

open System.IO
open Suave
open Suave.Files
open Suave.Filters
open Suave.Operators
open Suave.RequestErrors
open FunSharp.ArtStudio.Server.Helpers
open FunSharp.ArtStudio.Server.WebParts

module Routing =

    let routing secrets persistence deviantArtClient soraClient =
        
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
            
            POST >=> path $"{apiBase}/stash" >=> stash persistence deviantArtClient soraClient
            POST >=> path $"{apiBase}/publish" >=> publish persistence deviantArtClient secrets
            
            // TODO: can client just do all the calls for any of these?
            POST >=> path $"{apiBase}/inspiration2prompt" >=> inspiration2Prompt persistence
            POST >=> path $"{apiBase}/inspiration2gpt" >=> inspiration2ChatGPTTask persistence
            POST >=> path $"{apiBase}/prompt2deviation" >=> prompt2Deviation persistence
            POST >=> path $"{apiBase}/prompt2sora" >=> prompt2SoraTask persistence
            POST >=> path $"{apiBase}/retry-sora" >=> retrySora persistence
            POST >=> path $"{apiBase}/abort-task" >=> abortTask persistence
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
