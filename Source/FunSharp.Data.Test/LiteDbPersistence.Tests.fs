namespace FunSharp.Data.Test

open System
open System.IO
open FunSharp.DeviantArt
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
    
    let createPersistence<'T, 'Id when 'T : not struct and 'T : equality and 'T: not null>
        (databaseFilePath: string, collectionName: string) =
        
        if File.Exists databaseFilePath then File.Delete databaseFilePath
        LiteDbPersistence<'T, 'Id>(databaseFilePath, collectionName)
    
    [<Fact>]
    let ``FindAll() for new database should return no items`` () =
    
        // Arrange
        let persistence = createPersistence<TestModel, Guid>("test.db", "test")
        
        // Act
        let result = persistence.FindAll()
        
        // Assert
        %result.Should().BeEmpty()
        
    [<Fact>]
    let ``Find() after inserting an item should return that item`` () =
    
        // Arrange
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        let persistence = createPersistence<TestModel, Guid>("test.db", "test")
        
        %persistence.Insert(testItem.Id, testItem)
        
        // Act
        let result = persistence.Find(testItem.Id)
        
        // Assert
        %result.Should().BeSome()
        %result.Value.Should().Be(testItem)
        
    [<Fact>]
    let ``GetAll() after inserting an item should return a single-item collection with that item`` () =
    
        // Arrange
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        let persistence = createPersistence<TestModel, Guid>("test.db", "test")
        
        %persistence.Upsert(testItem.Id, testItem)
        
        // Act
        let result = persistence.FindAll()
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
