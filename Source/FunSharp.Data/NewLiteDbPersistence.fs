namespace FunSharp.Data

open FunSharp.Common
open FunSharp.Data.Abstraction
open LiteDB

type NewLiteDbPersistence(databaseFilePath: string) =
    
    let db = new LiteDatabase($"Filename={databaseFilePath};Mode=Exclusive")
    
    let withCollection (collectionName: string) flush f =
        
        let collection = db.GetCollection<BsonDocument>(collectionName)
        let result = f collection
        if flush then db.Checkpoint()
        result
        
    member private _.AsBsonDoc(value: 'T) =
        
        let doc = BsonDocument()
        doc["data"] <- JsonSerializer.serialize value
        doc
        
    member private _.AsValue<'T>(doc: BsonDocument) =
        
        doc["data"].AsString |> JsonSerializer.deserialize<'T>
        
    member private _.TryAsValue<'T>(doc: BsonDocument) =
        
        doc["data"].AsString |> JsonSerializer.tryDeserialize<'T>
        
    member private _.AsBsonValue(key: 'Key) =
        
        BsonValue(key.ToString())
        
    member private this.FindAll<'Value>(collection: ILiteCollection<BsonDocument>) : 'Value array =
        
        collection.FindAll()
        |> Seq.toArray
        |> Array.map this.TryAsValue<'Value>
        |> Array.choose (function | Some v -> Some v | None -> None)
        
    interface IPersistence with
        
        member _.Dispose() =
            
            db.Dispose()
            
        member this.Insert(collectionName, key: 'Key, value: 'Value) =
            
            withCollection collectionName true _.Insert(this.AsBsonValue key, this.AsBsonDoc value)
            
        member this.Update(collectionName, key: 'Key, value: 'Value) =
            
            withCollection collectionName true _.Update(this.AsBsonValue key, this.AsBsonDoc value)
            
        member this.Upsert(collectionName, key: 'Key, value: 'Value) =
            
            match withCollection collectionName true _.Upsert(this.AsBsonValue key, this.AsBsonDoc value) with
            | true -> UpsertResult.Insert
            | false -> UpsertResult.Update
            
        member this.Find(collectionName, key: 'Key) =
            
            let findById (collection: ILiteCollection<BsonDocument>) : 'Value option =
                collection.FindById(this.AsBsonValue key)
                |> Option.ofObj
                |> Option.map this.AsValue
                
            withCollection collectionName false findById
            
        member this.FindAll(collectionName) =
            
            withCollection collectionName false this.FindAll<'Value>
            
        member this.FindAny(collectionName, query) : 'Value array =
            
            let this = this :> IPersistence
            this.FindAll(collectionName) |> Array.filter query
            
        member this.Delete(collectionName, key: 'Key) =
            
            withCollection collectionName true _.Delete(this.AsBsonValue key)

        member this.Exists(collectionName, key: 'Key) =
            
            withCollection collectionName false _.Exists(this.AsBsonValue key)
