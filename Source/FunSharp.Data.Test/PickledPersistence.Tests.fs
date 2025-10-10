namespace FunSharp.Data.Test

open System
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
    
    [<Fact>]
    let ``Find() after inserting an item should return that item`` () =
    
        // Arrange
        let id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
        
        let testItem = MyDU.A {
            Id = id
            Text = "abc"
            Number = 123
        }
        
        use persistence = Helpers.createPickledPersistence("testDatabase")
        
        %persistence.Insert("testCollection", id, testItem)
        
        // Act
        let result = persistence.Find("testCollection", id)
        
        // Assert
        %result.Should().BeSome()
        %result.Value.Should().Be(testItem)
        
    [<Fact>]
    let ``FindAny() with one match returns an array with one item`` () =
    
        // Arrange
        let testItem : TestModelA = {
            Id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
            Text = "abc"
            Number = 123
        }
        
        use persistence = Helpers.createPickledPersistence("testDatabase")
        
        %persistence.Insert("testCollection", testItem.Id, testItem)
        
        // Act
        let result = persistence.FindAny<TestModelA>("testCollection", (fun x -> x.Text = "abc"))
        
        // Assert
        %result.Should().HaveLength(1)
        %result[0].Should().Be(testItem)
    
    [<Fact>]
    let ``FindAll() for new database should return no items`` () =
    
        // Arrange
        use persistence = Helpers.createPickledPersistence("testDatabase")
        
        // Act
        let result = persistence.FindAll<MyDU>("testCollection")
        
        // Assert
        %result.Should().BeEmpty()
        
    [<Fact>]
    let ``FindAll() after inserting an item should return a single-item collection with that item`` () =
    
        // Arrange
        let id = Guid.Parse "44b8ae0d-37b3-4be3-8992-e7f6832b472a"
        
        let testItem = MyDU.B {
            Id = id
            Text = "abc"
            Timestamp = DateTimeOffset(2023, 12, 25, 15, 30, 0, TimeSpan.FromHours(-5.0))
        }
        
        use persistence = Helpers.createPickledPersistence("testDatabase")
        
        %persistence.Upsert("testCollection", id, testItem)
        
        // Act
        let result = persistence.FindAll("testCollection")
        
        // Assert
        %result.Should().HaveLength(1)
        %(result |> Array.head).Should().Be(testItem)
