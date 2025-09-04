namespace FunSharp.Data

open LiteDB

type LiteDbPersistence<'Key, 'Value when 'Value : not struct and 'Value : equality and 'Value : not null>
    (databaseFilePath: string) =
    
    let mapper = FSharpBsonMapper()
    
    do mapper.EnsureRecord<'Value>() |> ignore

    let withCollection (collectionName: string) f =
        
        use db = new LiteDatabase(databaseFilePath, mapper)
        let collection = db.GetCollection<'T>(collectionName)
        f collection
    
    member _.Insert(collectionName, key: 'Key, value: 'Value) =
        withCollection collectionName _.Insert(BsonValue(key), value)
        
    member _.Update(collectionName, key: 'Key, value: 'Value) =
        withCollection collectionName _.Update(BsonValue(key), value)
        
    member _.Upsert(collectionName, key: 'Key, value: 'Value) =
        withCollection collectionName _.Upsert(BsonValue(key), value)
        
    member _.Find(collectionName, key: 'Key) : 'Value option =
        withCollection collectionName (fun collection -> collection.FindById(BsonValue(key)) |> Option.ofObj)
        
    member _.Delete(collectionName, key: 'Key) =
        withCollection collectionName _.Delete(BsonValue(key))
        
    member _.FindAll(collectionName) : 'Value array =
        withCollection collectionName (fun collection -> collection.FindAll() |> Seq.toArray)
