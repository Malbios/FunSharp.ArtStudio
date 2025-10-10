namespace FunSharp.Data.Test

open System
open Xunit
open Faqt
open Faqt.Operators

[<Trait("Category", "Standard")>]
module ``NewLiteDbPersistence Tests`` =
        
    [<Fact>]
    let ``Find() after inserting an item should return that item`` () =
    
        // Arrange
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        use persistence = Helpers.createNewLiteDbPersistence("testDatabase")
        
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
        
        use persistence = Helpers.createNewLiteDbPersistence("testDatabase")
        
        %persistence.Insert("testCollection", testItem.Id, testItem)
        
        // Act
        let result = persistence.FindAny<TestModel>("testCollection", (fun x -> x.Text = "abc"))
        
        // Assert
        %result.Should().HaveLength(1)
        %result[0].Should().Be(testItem)
    
    [<Fact>]
    let ``FindAll() for new database should return no items`` () =
    
        // Arrange
        use persistence = Helpers.createNewLiteDbPersistence("testDatabase")
        
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
        
        use persistence = Helpers.createNewLiteDbPersistence("testDatabase")
        
        %persistence.Upsert("testCollection", testItem.Id, testItem)
        
        // Act
        let result = persistence.FindAll("testCollection")
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
        
    [<Fact>]
    let ``Can store and retrieve items with option - some`` () =
    
        // Arrange
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Age = Some 134
        }
        
        let id = testItem.Id
        
        use persistence = Helpers.createNewLiteDbPersistence("testDatabase")
        
        %persistence.Insert("testCollection", id, testItem)
        
        // Act
        let result = persistence.FindAll("testCollection")
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
        
    [<Fact>]
    let ``Can store and retrieve items with option - none`` () =
    
        // Arrange
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Age = None
        }
        
        let id = testItem.Id
        
        use persistence = Helpers.createNewLiteDbPersistence("testDatabase")
        
        %persistence.Insert("testCollection", id, testItem)
        
        // Act
        let result = persistence.FindAll("testCollection")
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
        
    [<Fact>]
    let ``Can store and retrieve DU - simple`` () =
    
        // Arrange
        let id = Guid("44b8ae0d-37b3-4be3-8992-e7f6832b472a")
        let testItem = TestDU.CaseSimple
        
        use persistence = Helpers.createNewLiteDbPersistence("testDatabase")
        
        %persistence.Insert("testCollection", id, testItem)
        
        // Act
        let result = persistence.FindAll("testCollection")
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
        
    [<Fact>]
    let ``Can store and retrieve DU - string`` () =
    
        // Arrange
        let id = Guid("44b8ae0d-37b3-4be3-8992-e7f6832b472a")
        let testItem = TestDU.CaseString "abc"
        
        use persistence = Helpers.createNewLiteDbPersistence("testDatabase")
        
        %persistence.Insert("testCollection", id, testItem)
        
        // Act
        let result = persistence.FindAll("testCollection")
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
        
    [<Fact>]
    let ``Can store and retrieve DU - tuple`` () =
    
        // Arrange
        let id = Guid("44b8ae0d-37b3-4be3-8992-e7f6832b472a")
        let testItem = TestDU.CaseTuple (123, "yep")
        
        use persistence = Helpers.createNewLiteDbPersistence("testDatabase")
        
        %persistence.Insert("testCollection", id, testItem)
        
        // Act
        let result = persistence.FindAll("testCollection")
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
        
    [<Fact>]
    let ``Can store and retrieve DU - complex`` () =
    
        // Arrange
        let testItem = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        let id = testItem.Id
        let testItem = TestDU.CaseComplex testItem
        
        use persistence = Helpers.createNewLiteDbPersistence("testDatabase")
        
        %persistence.Insert("testCollection", id, testItem)
        
        // Act
        let result = persistence.FindAll("testCollection")
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
        
    [<Fact>]
    let ``Can have mixed types`` () =
    
        // Arrange
        let testItemA = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        let testItemB = {
            Identifier = Guid.Parse "71767ce4-1446-4bf0-b4f6-13ba217cb4a3"
            IsTrue = true
        }
        
        use persistence = Helpers.createNewLiteDbPersistence("testDatabase")
        
        %persistence.Insert("testCollection", testItemA.Id, testItemA)
        %persistence.Insert("testCollection", testItemB.Identifier, testItemB)
        
        // Act
        let resultA = persistence.FindAll<TestModel>("testCollection")
        let resultB = persistence.FindAll<TestModelAlt>("testCollection")
        
        // Assert
        %resultA.Should().HaveLength(1)
        %(resultA |> Array.head).Should().Be(testItemA)
        
        %resultB.Should().HaveLength(1)
        %(resultB |> Array.head).Should().Be(testItemB)
