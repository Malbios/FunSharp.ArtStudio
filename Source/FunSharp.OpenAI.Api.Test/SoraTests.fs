namespace FunSharp.OpenAI.Api.Test

open Xunit
open Faqt
open Faqt.Operators
open FunSharp.OpenAI.Api.Sora
open FunSharp.OpenAI.Api.Model.Sora

[<Trait("Category", "OnDemand")>]
module ``Sora Tests`` =
    
    [<Fact>]
    let ``Create image`` () =
    
        // Arrange
        let variant = AspectRatio.Square
        let prompt =
            """
            hyperrealistic cat baker
            baking "chocolate chip cookies"
            """
            
        use client = new Client()
        client.UpdateAuthTokens() |> Async.RunSynchronously
        
        // Act
        let result =
            client.UpdateAuthTokens()
            |> Async.bind (fun () -> client.CreateImage(prompt, variant))
            |> Async.RunSynchronously
        
        // Assert
        %result.Should().NotBeEmpty()
        %result.Should().StartWith("task_")
