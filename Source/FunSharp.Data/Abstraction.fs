namespace FunSharp.Data

module Abstraction =
    
    type UpsertResult =
        | Insert
        | Update
    
    type IPersistence =
        
        abstract member Dispose : unit -> unit
        
        abstract member Insert<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            : string * 'Key * 'Value -> unit
            
        abstract member Update<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            : string * 'Key * 'Value -> bool
            
        abstract member Upsert<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            : string * 'Key * 'Value -> UpsertResult
            
        abstract member Find<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            : string * 'Key -> 'Value option
            
        abstract member FindAll<'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            : string -> 'Value array
            
        abstract member Delete<'Key> : string * 'Key -> bool

    type IAuthPersistence<'T> =
        
        abstract member Load : unit -> 'T option
        abstract member Save : 'T -> unit
