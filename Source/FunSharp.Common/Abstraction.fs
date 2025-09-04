namespace FunSharp.Common

module Abstraction =
    
    type IPersistence =
        
        abstract member Insert<'Id, 'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            : string * 'Id * 'Value -> unit
            
        abstract member Update<'Id, 'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            : string * 'Id * 'Value -> bool
            
        abstract member Upsert<'Id, 'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            : string * 'Id * 'Value -> bool
            
        abstract member Find<'Id, 'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            : string * 'Id -> 'Value option
            
        abstract member FindAll<'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            : string -> 'Value array
            
        abstract member Delete<'Id> : string * 'Id -> bool

    type IAuthPersistence<'T> =
        
        abstract member Load : unit -> 'T option
        abstract member Save : 'T -> unit
