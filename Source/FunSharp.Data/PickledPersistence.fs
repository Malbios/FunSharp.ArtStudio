namespace FunSharp.Data

open System
open LiteDB
open MBrace.FsPickler
open FunSharp.Data.Abstraction

type PickledPersistence(databaseFilePath: string) =
    
    let pickler = FsPickler.CreateBinarySerializer()
    let persistence = new LiteDbPersistence(databaseFilePath) :> IPersistence
        
    member private _.AsBson<'T>(value: 'T) =
        let doc = BsonDocument()
        doc["data"] <- pickler.Pickle value
        doc
        
    member private _.AsValue<'T>(doc: BsonDocument) =
        pickler.UnPickle<'T> doc["data"]
        
    member private this.FindAll<'Value when 'Value: not struct and 'Value: not null>
        collectionName =
            persistence.FindAll(collectionName) |> Seq.toArray |> Array.map this.AsValue<'Value>
        
    interface IDisposable with
        
        member this.Dispose() =
            persistence.Dispose()
        
    interface IPersistence with
    
        member this.Insert(collectionName, key: 'Key, value: 'Value) =
                
            persistence.Insert(collectionName, key, value |> this.AsBson)
                
        member this.Update(collectionName, key: 'Key, value: 'Value) =
                
            persistence.Update(collectionName, key, value |> this.AsBson)
                
        member this.Upsert(collectionName, key: 'Key, value: 'Value) =
                
            persistence.Upsert(collectionName, key, value |> this.AsBson)
                
        member this.Find<'Key, 'Value when 'Value: not struct and 'Value: not null>
            (collectionName, key: 'Key) =
                
            persistence.Find(collectionName, key) |> Option.map this.AsValue<'Value>
                
        member this.FindAny<'Value when 'Value: not struct and 'Value: not null>
            (collectionName, query) =
                
            this.FindAll<'Value>(collectionName) |> Array.filter query
                
        member this.FindAll<'Value when 'Value: not struct and 'Value: not null>
            collectionName =
                
            this.FindAll<'Value>(collectionName)
                
        member _.Delete<'Key> (collectionName, key: 'Key) =
            
            persistence.Delete(collectionName, key)
            
        member this.Exists(collectionName, key: 'Key) =
            
            persistence.Exists(collectionName, key)

type SingleValuePickledPersistence<'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
    (databaseFilePath: string, key: string) =
    
    let persistence = new PickledPersistence(databaseFilePath) :> IPersistence
    
    member _.Upsert (value: 'Value) =
        
        persistence.Upsert(key, key, value)
        
    member _.Find() : 'Value option =
        
        persistence.Find(key, key)
