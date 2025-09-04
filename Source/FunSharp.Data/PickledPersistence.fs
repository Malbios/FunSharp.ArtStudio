namespace FunSharp.Data

open FunSharp.Common.Abstraction
open LiteDB
open MBrace.FsPickler

type PickledPersistence(databaseFilePath: string) =
    
    let pickler = FsPickler.CreateBinarySerializer()
    let persistence = LiteDbPersistence(databaseFilePath)
        
    member _.AsBson<'T>(value: 'T) =
        let doc = BsonDocument()
        doc["data"] <- pickler.Pickle value
        doc
        
    member _.AsValue<'T>(doc: BsonDocument) =
        pickler.UnPickle<'T> doc["data"]
        
    interface IPersistence with
    
        member this.Insert<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            (collectionName, key: 'Key, value: 'Value) =
                persistence.Insert(collectionName, key, value |> this.AsBson)
                
        member this.Update<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            (collectionName, key: 'Key, value: 'Value) =
                persistence.Update(collectionName, key, value |> this.AsBson)
                
        member this.Upsert<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            (collectionName, key: 'Key, value: 'Value) =
                persistence.Upsert(collectionName, key, value |> this.AsBson)
                
        member this.Find<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            (collectionName, key: 'Key) =
                persistence.Find(collectionName, key) |> Option.map this.AsValue<'Value>
                
        member this.FindAll<'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
            collectionName =
                persistence.FindAll(collectionName) |> Seq.toArray |> Array.map this.AsValue<'Value>
                
        member _.Delete<'Key> (collectionName, key: 'Key) =
            persistence.Delete(collectionName, key)
            
type SingleValuePickledPersistence<'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
    (databaseFilePath: string, key: string) =
    
    let persistence : IPersistence = PickledPersistence(databaseFilePath)
    
    member _.Upsert (value: 'Value) =
        persistence.Upsert(key, key, value)
        
    member _.Find() : 'Value option =
        persistence.Find(key, key)
