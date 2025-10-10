namespace FunSharp.Data.Test

open System
open FunSharp.Common
open LiteDB
open Xunit
open Faqt
open Faqt.Operators
    
[<Trait("Category", "OnDemand")>]
module ``LiteDbPersistence OnDemand Tests`` =
        
    [<Fact>]
    let ``Complex Object Serialization Test 1`` () =
    
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        let testValue = TestDU.CaseComplex testItem
        
        Helpers.deleteExisting "testDatabase"
        use db = new LiteDatabase("testDatabase.db")
        
        let key = testItem.Id.ToString()
        let bsonKey = BsonValue(key)
        let doc = BsonDocument()
        let testValueString = testValue |> JsonSerializer.serialize
        doc["data"] <- testValueString
        let collection = db.GetCollection<BsonDocument>("testCollection")
        collection.Insert(bsonKey, doc)
        let result = collection.FindById(BsonValue(key)) |> Option.ofObj
        
        %result.Should().BeSome()
        let resultValue = result.Value["data"].AsString
        %(resultValue |> JsonSerializer.deserialize<TestDU>).Should().Be(testValue)
        
    [<Fact>]
    let ``Complex Object Serialization Test 2`` () =
    
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        let testValue : TestDU = TestDU.CaseComplex testItem
        
        use persistence = Helpers.createLiteDbPersistence("testDatabase")
        
        let doc = BsonDocument()
        doc["data"] <- testValue |> JsonSerializer.serialize
        %persistence.Insert("testCollection", testItem.Id.ToString(), doc)
        
        let result =
            persistence.Find<string, BsonDocument>("testCollection", testItem.Id.ToString())
            |> Option.map (fun x -> x["data"].AsString |> JsonSerializer.deserialize<TestDU>)
            
        %result.Should().BeSome()
        %result.Value.Should().Be(testValue)

[<Trait("Category", "Standard")>]
module ``LiteDbPersistence Tests`` =
        
    [<Fact>]
    let ``Find() after inserting an item should return that item`` () =
    
        // Arrange
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        use persistence = Helpers.createLiteDbPersistence("testDatabase")
        
        %persistence.Insert("testCollection", testItem.Id, testItem)
        
        // Act
        let result = persistence.Find("testCollection", testItem.Id)
        
        // Assert
        %result.Should().BeSome()
        %result.Value.Should().Be(testItem)
        
    [<Fact>]
    let ``FindAny() with one match returns an array with one item`` () =
    
        // Arrange
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        use persistence = Helpers.createLiteDbPersistence("testDatabase")
        
        %persistence.Insert("testCollection", testItem.Id, testItem)
        
        // Act
        let result = persistence.FindAny<TestModel>("testCollection", (fun x -> x.Text = "abc"))
        
        // Assert
        %result.Should().HaveLength(1)
        %result[0].Should().Be(testItem)
    
    [<Fact>]
    let ``FindAll() for new database should return no items`` () =
    
        // Arrange
        use persistence = Helpers.createLiteDbPersistence("testDatabase")
        
        // Act
        let result = persistence.FindAll("testCollection")
        
        // Assert
        %result.Should().BeEmpty()
        
    [<Fact>]
    let ``FindAll() after inserting an item should return a single-item collection with that item`` () =
    
        // Arrange
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        use persistence = Helpers.createLiteDbPersistence("testDatabase")
        
        %persistence.Upsert("testCollection", testItem.Id, testItem)
        
        // Act
        let result = persistence.FindAll("testCollection")
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
