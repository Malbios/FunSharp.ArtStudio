namespace FunSharp.DeviantArt.Server

open FunSharp.Data
open FunSharp.Data.Abstraction
open FunSharp.DeviantArt.Api.Model

[<RequireQualifiedAccess>]
module Persistence =
    
    type AuthenticationPersistence() =
        let persistence = SingleValuePickledPersistence<AuthenticationData>("persistence.db", "authentication")
        
        interface IAuthPersistence<AuthenticationData> with
            
            member this.Load() =
                
                persistence.Find()
            
            member this.Save(value) =
                
                persistence.Upsert(value) |> ignore
