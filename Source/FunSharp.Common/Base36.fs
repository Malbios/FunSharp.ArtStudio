namespace FunSharp.Common

[<RequireQualifiedAccess>]
module Base36 =
    
    [<Literal>]
    let chars = "0123456789abcdefghijklmnopqrstuvwxyz"
    
    let encode (value: int64) =
        
        let base36 = int64 chars.Length

        let rec encode acc n =
            if n = 0L then acc
            else
                let index = int (n % base36)
                let c = chars[index]
                encode (string c + acc) (n / base36)

        match value with
        | 0L -> "0"
        | n when n < 0L -> "-" + encode "" (-n)
        | n -> encode "" n
