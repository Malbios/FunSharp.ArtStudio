namespace FunSharp.DeviantArt

open System.IO
open FunSharp.DeviantArt.Api
open Newtonsoft.Json
open LiteDB

[<RequireQualifiedAccess>]
module Persistence =
    
    type File<'T>() =
        
        let filePath = ".persistence"
        
        let read () =
            File.ReadAllText filePath
        
        override this.ToString() =
            read ()
        
        interface IPersistence<'T> with
        
            member _.Load() =
                
                if File.Exists filePath then
                    read ()
                    |> JsonConvert.DeserializeObject<'T>
                    |> Some
                else
                    None
                
            member _.Save(value: 'T) =
                
                JsonConvert.SerializeObject value
                |> fun x -> File.WriteAllText(filePath, x)
    
    type Memory<'T>() =
        
        let mutable persistence = ""
        
        override this.ToString() =
            persistence
        
        interface IPersistence<'T> with
        
            member _.Load() =
                
                if persistence = "" then
                    None
                else
                    persistence
                    |> JsonConvert.DeserializeObject<'T>
                    |> Some
                
            member _.Save(value: 'T) =
                
                persistence <- JsonConvert.SerializeObject value
                
    type private DbDocument<'T when 'T: equality and 'T : not struct> = {
        Key: string
        Value: 'T
    }
                
    type LiteDb<'T when 'T: equality and 'T : not struct>(filePath: string, collectionName: string) =
        
        member _.Save(values: (string * 'T) array) =
            
            use db = new LiteDatabase(filePath)
            let collection = db.GetCollection<DbDocument<'T>>(collectionName)
            
            values
            |> Array.map(fun (key, value) -> { Key = key; Value = value })
            |> collection.Upsert
            |> ignore
            
        member _.Load() =
            
            use db = new LiteDatabase(filePath)
            let collection = db.GetCollection<DbDocument<'T>>(collectionName)
            
            collection.FindAll()
            |> Array.ofSeq
            |> Array.map(_.Value)
