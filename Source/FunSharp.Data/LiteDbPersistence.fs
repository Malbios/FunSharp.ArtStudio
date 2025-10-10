namespace FunSharp.Data

open FunSharp.Data.Abstraction
open LiteDB

type LiteDbPersistence(databaseFilePath: string) =
    
    let mapper = FSharpBsonMapper()
    let db = new LiteDatabase(databaseFilePath, mapper)

    let withCollection (collectionName: string) f =
        
        let collection = db.GetCollection<'T>(collectionName)
        f collection
            
    interface IPersistence with
        
        member _.Dispose() =
            
            db.Dispose()
    
        member _.Insert(collectionName, key: 'Key, value: 'Value) =
                
            mapper.EnsureRecord<'Value>() |> ignore
            let key = BsonValue(key)
            withCollection collectionName _.Insert(key, value)
            
        member _.Update(collectionName, key: 'Key, value: 'Value) =
                
            mapper.EnsureRecord<'Value>() |> ignore
            withCollection collectionName _.Update(BsonValue(key), value)
            
        member _.Upsert(collectionName, key: 'Key, value: 'Value) =
                
            mapper.EnsureRecord<'Value>() |> ignore
            match withCollection collectionName _.Upsert(BsonValue(key), value) with
            | true -> UpsertResult.Insert
            | false -> UpsertResult.Update
            
        member _.Find<'Key, 'Value when 'Value: not struct and 'Value: not null>
            (collectionName, key: 'Key) : 'Value option =
                
            mapper.EnsureRecord<'Value>() |> ignore
            withCollection collectionName (fun collection -> collection.FindById(BsonValue(key)) |> Option.ofObj)
                
        member this.FindAny<'Value when 'Value: not struct and 'Value: not null>
            (collectionName, query) : 'Value array =
                
            let this = this :> IPersistence
            this.FindAll(collectionName) |> Array.filter query

        member _.FindAll<'Value when 'Value: not struct and 'Value: not null>
            collectionName : 'Value array =
                
            mapper.EnsureRecord<'Value>() |> ignore
            withCollection collectionName (fun collection -> collection.FindAll() |> Seq.toArray)
            
        member _.Delete<'Key>(collectionName, key: 'Key) =
            
            withCollection collectionName _.Delete(BsonValue(key))

        member this.Exists(collectionName, key: 'Key) =
            
            withCollection collectionName _.Exists(BsonValue(key))
