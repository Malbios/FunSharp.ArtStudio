namespace FunSharp.Data.Test

open System
open System.IO
open Xunit
open Faqt
open Faqt.Operators
open FunSharp.Data
open FunSharp.Data.Abstraction

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
    
    let createPersistence(databaseFilePath: string) =
        
        if File.Exists databaseFilePath then File.Delete databaseFilePath
        new PickledPersistence(databaseFilePath) :> IPersistence
    
    [<Fact>]
    let ``FindAll() for new database should return no items`` () =
    
        // Arrange
        let persistence = createPersistence("test.db")
        
        // Act
        let result = persistence.FindAll<MyDU> "test"
        
        // Assert
        %result.Should().BeEmpty()
        persistence.Dispose()
        
    [<Fact>]
    let ``Find() after inserting an item should return that item`` () =
    
        // Arrange
        let id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
        
        let testItem = MyDU.A {
            Id = id
            Text = "abc"
            Number = 123
        }
        
        let persistence = createPersistence("test.db")
        
        %persistence.Insert("test", id, testItem)
        
        // Act
        let result = persistence.Find("test", id)
        
        // Assert
        %result.Should().BeSome()
        %result.Value.Should().Be(testItem)
        persistence.Dispose()
        
    [<Fact>]
    let ``GetAll() after inserting an item should return a single-item collection with that item`` () =
    
        // Arrange
        let id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
        
        let testItem = MyDU.B {
            Id = id
            Text = "abc"
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        let persistence = createPersistence("test.db")
        
        %persistence.Upsert("test", id, testItem)
        
        // Act
        let result = persistence.FindAll("test")
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
        persistence.Dispose()
