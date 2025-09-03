namespace FunSharp.DeviantArt.Authenticator

open FunSharp.DeviantArt
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Api

[<RequireQualifiedAccess>]
module Persistence =
    
    type AuthenticationPersistence() =
        let persistence = PickledSinglePersistence<AuthenticationData>("persistence.db", "authentication")
        
        interface IPersistence<AuthenticationData> with
            
            member this.Load() =
                
                persistence.Find()
            
            member this.Save(value) =
                
                persistence.Upsert(value) |> ignore
