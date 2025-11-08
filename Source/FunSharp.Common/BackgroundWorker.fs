namespace FunSharp.Common

open System
open System.Threading
open System.Threading.Tasks

type BackgroundWorker(ct: CancellationToken, randomDelay: int * int, action: unit -> Async<unit>) =
    
    let rng = Random()
    
    member private this.RepeatOrFinish() =
        
        if not ct.IsCancellationRequested then
            this.Work()
        else
            printfn "background worker cancelled"
            Task.FromResult(())
            
    member this.Work() = task {
        try
            do! action ()
        with ex ->
            printfn $"background worker action failed: {ex}"
        
        let delayMs = rng.Next(fst randomDelay, snd randomDelay)
        do! Task.Delay(delayMs, ct)
        
        return! this.RepeatOrFinish()
    }
