namespace FunSharp.Common

open System.IO

[<RequireQualifiedAccess>]
module Stream =
    
    let readAllBytesAsync (stream: Stream) : Async<byte[]> = async {
        let buffer =
            match stream.Length with
            | length when length <= int64 System.Int32.MaxValue -> int length
            | _ -> failwith "The file is too large for a single buffer"
            |> Array.zeroCreate<byte>
        
        let! _ = stream.ReadAsync(buffer, 0, buffer.Length) |> Async.AwaitTask
        
        return buffer
    }
