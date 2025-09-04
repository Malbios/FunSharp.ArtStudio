namespace FunSharp.Data

open LiteDB
open MBrace.FsPickler

type PickledPersistence(databaseFilePath: string) =
    
    let pickler = FsPickler.CreateBinarySerializer()
    let persistence = LiteDbPersistence<string, BsonDocument>(databaseFilePath)
    
    let asBsonDocument (value: 'Value) =
        let doc = BsonDocument()
        doc["data"] <- pickler.Pickle value
        doc
        
    let asValue (doc: BsonDocument) =
        pickler.UnPickle<'T> doc["data"]
    
    member _.Insert<'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
        (collectionName, key: string, value: 'Value) =
            persistence.Insert(collectionName, key, asBsonDocument value)
        
    member _.Update<'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
        (collectionName, key: string, value: 'Value) =
            persistence.Update(collectionName, key, asBsonDocument value)
        
    member _.Upsert<'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
        (collectionName, key: string, value: 'Value) =
            persistence.Upsert(collectionName, key, asBsonDocument value)
        
    member _.Find<'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
        (collectionName, key: string) : 'Value option =
            persistence.Find(collectionName, key) |> Option.map asValue
        
    member _.Delete(collectionName, key: string) =
        persistence.Delete(collectionName, key)
        
    member _.FindAll<'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
        collectionName : 'Value array =
            persistence.FindAll(collectionName) |> Seq.toArray |> Array.map asValue

type PickledSinglePersistence<'Value when 'Value : not struct and 'Value : equality and 'Value: not null>
    (databaseFilePath: string, key: string) =
    
    let persistence = PickledPersistence(databaseFilePath)
        
    member _.Upsert(value: 'Value) =
        persistence.Upsert(key, key, value)
        
    member _.Find() =
        persistence.Find(key, key)
