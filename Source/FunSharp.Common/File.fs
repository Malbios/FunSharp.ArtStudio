namespace FunSharp.Common

open System.IO
open System.Web

[<RequireQualifiedAccess>]
module File =
    
    let readAllBytesAsync (path: string) : Async<byte[]> = async {
        use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize = 64 * 1024, options = FileOptions.Asynchronous)
        
        return! Stream.readAllBytesAsync stream
    }
    
    let writeAllBytesAsync (path: string) (bytes: byte array) =
        
        File.WriteAllBytesAsync(path, bytes) |> Async.AwaitTask
