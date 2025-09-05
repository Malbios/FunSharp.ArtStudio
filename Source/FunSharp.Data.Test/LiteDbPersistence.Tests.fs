namespace FunSharp.Data.Test

open System
open System.IO
open FunSharp.Data
open Xunit
open Faqt
open Faqt.Operators

type TestModel = {
    Id: Guid
    Text: string
    Number: int
    Timestamp: DateTimeOffset
}

[<Trait("Category", "Standard")>]
module ``LiteDbPersistence Tests`` =
    
    let createPersistence(databaseFilePath: string) =
        
        if File.Exists databaseFilePath then File.Delete databaseFilePath
        LiteDbPersistence(databaseFilePath)
    
    [<Fact>]
    let ``FindAll() for new database should return no items`` () =
    
        // Arrange
        let persistence = createPersistence("test.db")
        
        // Act
        let result = persistence.FindAll("test")
        
        // Assert
        %result.Should().BeEmpty()
        persistence.Dispose()
        
    [<Fact>]
    let ``Find() after inserting an item should return that item`` () =
    
        // Arrange
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        let persistence = createPersistence("test.db")
        
        %persistence.Insert("test", testItem.Id, testItem)
        
        // Act
        let result = persistence.Find("test", testItem.Id)
        
        // Assert
        %result.Should().BeSome()
        %result.Value.Should().Be(testItem)
        persistence.Dispose()
        
    [<Fact>]
    let ``GetAll() after inserting an item should return a single-item collection with that item`` () =
    
        // Arrange
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        let persistence = createPersistence("test.db")
        
        %persistence.Upsert("test", testItem.Id, testItem)
        
        // Act
        let result = persistence.FindAll("test")
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
        persistence.Dispose()
