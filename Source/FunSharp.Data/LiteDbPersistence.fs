namespace FunSharp.Data

open LiteDB

type LiteDbPersistence(databaseFilePath: string) =
    
    let mapper = FSharpBsonMapper()

    let withCollection (collectionName: string) f =
        
        use db = new LiteDatabase(databaseFilePath, mapper)
        let collection = db.GetCollection<'T>(collectionName)
        f collection
    
    member _.Insert<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value : not null>
        (collectionName, key: 'Key, value: 'Value) =
            
            mapper.EnsureRecord<'Value>() |> ignore
            withCollection collectionName _.Insert(BsonValue(key), value)
        
    member _.Update<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value : not null>
        (collectionName, key: 'Key, value: 'Value) =
            mapper.EnsureRecord<'Value>() |> ignore
            withCollection collectionName _.Update(BsonValue(key), value)
        
    member _.Upsert<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value : not null>
        (collectionName, key: 'Key, value: 'Value) =
            mapper.EnsureRecord<'Value>() |> ignore
            withCollection collectionName _.Upsert(BsonValue(key), value)
        
    member _.Find<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value : not null>
        (collectionName, key: 'Key) : 'Value option =
            mapper.EnsureRecord<'Value>() |> ignore
            withCollection collectionName (fun collection -> collection.FindById(BsonValue(key)) |> Option.ofObj)
        
    member _.FindAll<'Value when 'Value : not struct and 'Value : equality and 'Value : not null>
        collectionName : 'Value array =
            mapper.EnsureRecord<'Value>() |> ignore
            withCollection collectionName (fun collection -> collection.FindAll() |> Seq.toArray)
        
    member _.Delete<'Key>(collectionName, key: 'Key) =
        withCollection collectionName _.Delete(BsonValue(key))
