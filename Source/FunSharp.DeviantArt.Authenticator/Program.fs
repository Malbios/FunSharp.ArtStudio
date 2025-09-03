namespace FunSharp.DeviantArt

open FunSharp.DeviantArt.Authenticator
open FunSharp.Common
open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model

module Program =
                    
    [<EntryPoint>]
    let main _ =
        let secrets = Secrets.load ()
        let authPersistence : IPersistence<AuthenticationData> = Persistence.AuthenticationPersistence()
        let client = Client(authPersistence, secrets.client_id, secrets.client_secret)
        
        let profile = client.WhoAmI() |> AsyncResult.getOrFail |> Async.RunSynchronously
        if profile.username = "" then
            failwith "Something went wrong! Could not read profile username."
        
        printfn $"Hello, {profile.username}!"
        printfn ""
        
        printfn $"{authPersistence.Load() |> JsonSerializer.serialize}"
        
        printfn "Bye!"
        
        0
