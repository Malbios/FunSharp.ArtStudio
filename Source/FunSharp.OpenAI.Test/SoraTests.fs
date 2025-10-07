namespace FunSharp.Data.Test

open Xunit
open Faqt
open Faqt.Operators
open FunSharp.OpenAI.Sora

[<Trait("Category", "OnDemand")>]
module ``Sora Tests`` =
    
    [<Fact>]
    let ``Create image`` () =
    
        // Arrange
        let variant = ImageType.Square
        let prompt =
            """
            hyperrealistic cat baker
            baking "chocolate chip cookies"
            """
            
        let client = Client()
        client.UpdateAuthTokens() |> Async.RunSynchronously
        
        // Act
        let result =
            client.UpdateAuthTokens()
            |> Async.bind (fun () -> client.CreateImage(prompt, variant))
            |> Async.RunSynchronously
        
        // Assert
        %result.Should().NotBeEmpty()
        %result.Should().StartWith("task_")
