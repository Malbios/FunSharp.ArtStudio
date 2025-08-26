namespace FunSharp.Common

module Result =
    
    let tee f result =
        
        match result with
        | Ok v ->
            f v
        | Error _ -> ()
            
        result
