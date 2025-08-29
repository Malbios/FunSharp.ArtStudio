namespace FunSharp.Data.Test

open System
open System.IO
open FunSharp.DeviantArt
open Xunit
open Faqt
open Faqt.Operators

type TestModelA = {
    Id: Guid
    Text: string
    Number: int
}

type TestModelB = {
    Id: Guid
    Text: string
    Timestamp: DateTimeOffset
}

type MyDU =
    | A of TestModelA
    | B of TestModelB

[<Trait("Category", "Standard")>]
module ``PickledPersistence Tests`` =
    
    let createPersistence<'T, 'Id when 'T : not struct and 'T : equality and 'T: not null>
        (databaseFilePath: string, collectionName: string) =
        
        if File.Exists databaseFilePath then File.Delete databaseFilePath
        PickledPersistence<'T, 'Id>(databaseFilePath, collectionName)
    
    [<Fact>]
    let ``FindAll() for new database should return no items`` () =
    
        // Arrange
        let persistence = createPersistence<MyDU, Guid>("test.db", "test")
        
        // Act
        let result = persistence.FindAll()
        
        // Assert
        %result.Should().BeEmpty()
        
    [<Fact>]
    let ``Find() after inserting an item should return that item`` () =
    
        // Arrange
        let id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
        
        let testItem = MyDU.A {
            Id = id
            Text = "abc"
            Number = 123
        }
        
        let persistence = createPersistence<MyDU, Guid>("test.db", "test")
        
        %persistence.Insert(id, testItem)
        
        // Act
        let result = persistence.Find(id)
        
        // Assert
        %result.Should().BeSome()
        %result.Value.Should().Be(testItem)
        
    [<Fact>]
    let ``GetAll() after inserting an item should return a single-item collection with that item`` () =
    
        // Arrange
        let id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
        
        let testItem = MyDU.B {
            Id = id
            Text = "abc"
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        let persistence = createPersistence<MyDU, Guid>("test.db", "test")
        
        %persistence.Upsert(id, testItem)
        
        // Act
        let result = persistence.FindAll()
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
