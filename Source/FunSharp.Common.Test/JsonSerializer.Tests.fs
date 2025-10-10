namespace FunSharp.Common.Test

open System
open FunSharp.Common
open Xunit
open Faqt
open Faqt.Operators

[<Trait("Category", "Standard")>]
module ``JsonSerializer Tests`` =
    
    type TestData = {
        Id: Guid
        Name: string
        Age: int
    }
    
    type Sample =
        | CaseSimple
        | CaseString of string
        | CaseTuple of int * string
        | CaseComplex of TestData
    
    [<Fact>]
    let ``Can handle simple DU case`` () =
    
        // Arrange
        let testValue = Sample.CaseSimple
        
        // Act
        let text = testValue |> JsonSerializer.serialize
        let result = text |> JsonSerializer.deserialize<Sample>
        
        // Assert
        %result.Should().Be(testValue)
    
    [<Fact>]
    let ``Can handle string DU case`` () =
    
        // Arrange
        let testValue = Sample.CaseString "blob"
        
        // Act
        let text = testValue |> JsonSerializer.serialize
        let result = text |> JsonSerializer.deserialize<Sample>
        
        // Assert
        %result.Should().Be(testValue)
    
    [<Fact>]
    let ``Can handle tuple DU case`` () =
    
        // Arrange
        let testValue = Sample.CaseTuple (123, "bla")
        
        // Act
        let text = testValue |> JsonSerializer.serialize
        let result = text |> JsonSerializer.deserialize<Sample>
        
        // Assert
        %result.Should().Be(testValue)
    
    [<Fact>]
    let ``Can handle complex DU case`` () =
    
        // Arrange
        let complexValue = {
            Id = Guid("b3116d69-31fa-413c-9a20-87c74ec39430")
            Name = "Bobby Bobbing"
            Age = 42
        }
        
        let testValue = Sample.CaseComplex complexValue
        
        // Act
        let text = testValue |> JsonSerializer.serialize
        let result = text |> JsonSerializer.deserialize<Sample>
        
        // Assert
        %result.Should().Be(testValue)
