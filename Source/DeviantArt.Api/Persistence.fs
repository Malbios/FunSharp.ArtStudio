namespace DeviantArt.Api

open System.IO
open Newtonsoft.Json

[<RequireQualifiedAccess>]
module Persistence =
    
    type FilePersistence<'T>() =
        let persistenceFilePath = ".persistence"
        
        interface IPersistence<'T> with
        
            member _.Load() =
                if File.Exists persistenceFilePath then
                    File.ReadAllText persistenceFilePath
                    |> JsonConvert.DeserializeObject<'T>
                    |> Some
                else
                    None
                
            member _.Save(value: 'T) =
                JsonConvert.SerializeObject value
                |> fun x -> File.WriteAllText(persistenceFilePath, x)
