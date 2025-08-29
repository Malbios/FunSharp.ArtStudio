namespace FunSharp.DeviantArt

open System.IO
open FunSharp.DeviantArt.Api.Model
open Newtonsoft.Json
open LiteDB
open FunSharp.DeviantArt.Api
open FunSharp.Common

[<RequireQualifiedAccess>]
module Persistence =
    
    type AuthenticationPersistence() =
        let persistence = PickledSinglePersistence<AuthenticationData>("persistence.db", "authentication")
        
        interface IPersistence<AuthenticationData> with
            
            member this.Load() =
                
                persistence.Find()
            
            member this.Save(value) =
                
                persistence.Upsert(value) |> ignore
