namespace FunSharp.DeviantArt.Manager

open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model

type InMemoryAuthPersistence() =
    
    let mutable persistence : AuthenticationData option = None
        
    interface IPersistence<AuthenticationData> with
        
        member this.Load() =
            
            persistence
        
        member this.Save(value) =
            
            persistence <- Some value
