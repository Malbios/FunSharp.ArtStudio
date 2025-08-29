namespace FunSharp.DeviantArt

open LiteDB
open MBrace.FsPickler

type PickledPersistence<'T, 'Id when 'T : not struct and 'T : equality and 'T: not null>
    (databaseFilePath: string, collectionName: string) =
    
    let pickler = FsPickler.CreateBinarySerializer()
    let persistence = LiteDbPersistence<BsonDocument, 'Id>(databaseFilePath, collectionName)
    
    let doc (value: 'T) =
        let doc = BsonDocument()
        doc["data"] <- pickler.Pickle value
        doc
        
    let value (doc: BsonDocument) =
        pickler.UnPickle<'T> doc["data"]
    
    member _.Insert(id: 'Id, value: 'T) =
        
        persistence.Insert(id, doc value)
        
    member _.Update(id: 'Id, value: 'T) =
        
        persistence.Update(id, doc value)
        
    member _.Upsert(id: 'Id, value: 'T) =
        
        persistence.Upsert(id, doc value)
        
    member _.Find(id: 'Id) =
        
        persistence.Find(id) |> function
            | Some x -> value x |> Some
            | None -> None
        
    member _.Delete(id: 'Id) =
        
        persistence.Delete(id)
        
    member _.FindAll() =
        
        persistence.FindAll() |> Seq.toArray |> Array.map value

type PickledSinglePersistence<'T when 'T : not struct and 'T : equality and 'T: not null>
    (databaseFilePath: string, key: string) =
    
    let persistence = PickledPersistence<'T, string>(databaseFilePath, key)
        
    member _.Upsert(value: 'T) =
        
        persistence.Upsert(key, value)
        
    member _.Find() =
        
        persistence.Find(key)
