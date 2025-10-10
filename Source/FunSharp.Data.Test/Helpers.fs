namespace FunSharp.Data.Test

open System
open System.IO
open FunSharp.Data
open FunSharp.Data.Abstraction

type TestModel = {
    Id: Guid
    Text: string
    Number: int
    Timestamp: DateTimeOffset
}

type TestModelAlt = {
    Identifier: Guid
    IsTrue: bool
}

type TestDU =
    | CaseSimple
    | CaseString of string
    | CaseTuple of int * string
    | CaseComplex of TestModel
    
type TestModelWithOption = {
    Id: Guid
    Age: int option
}

[<RequireQualifiedAccess>]
module Helpers =
    
    let deleteExisting databaseName =
        
        [
            $"{databaseName}.db"
            $"{databaseName}-log.db"
        ]
        |> List.iter (fun x ->
            if File.Exists x then
                File.Delete x
        )
        
    let createNewLiteDbPersistence(databaseName: string) =
        
        deleteExisting databaseName
        new NewLiteDbPersistence($"{databaseName}.db") :> IPersistence
    
    let createLiteDbPersistence(databaseName: string) =
    
        deleteExisting databaseName
        new LiteDbPersistence($"{databaseName}.db") :> IPersistence
    
    let createPickledPersistence(databaseName: string) =
    
        deleteExisting databaseName
        new PickledPersistence($"{databaseName}.db") :> IPersistence
