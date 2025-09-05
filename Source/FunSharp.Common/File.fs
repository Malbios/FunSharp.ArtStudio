namespace FunSharp.Common

open System.IO

[<RequireQualifiedAccess>]
module File =
    
    let readAllBytesAsync (path: string) : Async<byte[]> = async {
        use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize = 64 * 1024, options = FileOptions.Asynchronous)
        
        return! Stream.readAllBytesAsync stream
    }
