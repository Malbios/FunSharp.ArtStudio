namespace FunSharp.Common

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module Task =

    let map (f: 'T -> 'U) (t: Task<'T>) : Task<'U> = task {
        let! x = t
        return f x
    }
