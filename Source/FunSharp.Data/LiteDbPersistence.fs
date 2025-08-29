namespace FunSharp.DeviantArt

open LiteDB

type LiteDbPersistence<'T, 'Id when 'T : not struct and 'T : equality and 'T: not null>
    (databaseFilePath: string, collectionName: string) =
    
    let mapper = FSharpBsonMapper()
    
    do mapper.EnsureRecord<'T>() |> ignore

    let withCollection f =
        
        use db = new LiteDatabase(databaseFilePath, mapper)
        let collection = db.GetCollection<'T>(collectionName)
        f collection
    
    member _.Insert (id: 'Id, item: 'T) =
        
        withCollection _.Insert(BsonValue(id), item)
        
    member _.Update (id: 'Id, item: 'T) =
        
        withCollection _.Update(BsonValue(id), item)
        
    member _.Upsert (id: 'Id, item: 'T) =
        
        withCollection _.Upsert(BsonValue(id), item)
        
    member _.Find (id: 'Id) =
        
        withCollection (fun collection -> collection.FindById(BsonValue(id)) |> Option.ofObj)
        
    member _.Delete (id: 'Id) =
        
        withCollection _.Delete(BsonValue(id))
        
    member _.FindAll () =
        
        withCollection (fun collection -> collection.FindAll() |> Seq.toArray)
