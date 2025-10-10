namespace FunSharp.Common.Test

open FunSharp.Common
open Xunit
open Faqt
open Faqt.Operators

[<Trait("Category", "Standard")>]
module ``String Tests`` =
    
    [<Fact>]
    let ``trim trims each line`` () =
    
        // Arrange
        let testValue =
            """
            this is
            some kind of test           
            """
            
        let expectedValue = "\nthis is\nsome kind of test\n"
        
        // Act
        let result = String.trim testValue
        
        // Assert
        %result.Should().Be(expectedValue)
