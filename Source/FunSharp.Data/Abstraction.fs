namespace FunSharp.Data

open System

module Abstraction =
    
    type UpsertResult =
        | Insert
        | Update
    
    type IPersistence =
        inherit IDisposable
        
        abstract member Insert : string * 'Key * 'Value -> unit
            
        abstract member Update : string * 'Key * 'Value -> bool
            
        abstract member Upsert : string * 'Key * 'Value -> UpsertResult
            
        abstract member Find<'Key, 'Value when 'Value: not struct and 'Value: not null> : string * 'Key -> 'Value option
            
        abstract member FindAny<'Value when 'Value: not struct and 'Value: not null> : string * ('Value -> bool) -> 'Value array
            
        abstract member FindAll<'Value when 'Value: not struct and 'Value: not null> : string -> 'Value array
            
        abstract member Delete : string * 'Key -> bool
        
        abstract member Exists : string * 'Key -> bool
