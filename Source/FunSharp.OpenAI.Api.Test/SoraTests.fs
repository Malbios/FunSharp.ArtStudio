namespace FunSharp.OpenAI.Api.Test

open System
open System.Linq
open Xunit
open Faqt
open Faqt.Operators
open Microsoft.Extensions.Logging
open FunSharp.OpenAI.Api.Sora
open FunSharp.OpenAI.Api.Model.Sora

[<Trait("Category", "OnDemand")>]
module ``Sora Tests`` =
    
    type Logger<'T>() =
        
        interface IDisposable with
        
            member _.Dispose() = ()
        
        interface ILogger<'T> with
        
            member _.Log(_, _, _, _, _) = ()
            member this.BeginScope _ = this
            member this.IsEnabled _ = true

    [<Fact>]
    let ``Create image`` () =
    
        // Arrange
        let variant = AspectRatio.Square
        let prompt =
            """
            hyperrealistic cat baker
            baking "chocolate chip cookies"
            """
            
        use logger = new Logger<Client>()
        use client = new Client(logger)
        
        client.UpdateAuthTokens() |> Async.RunSynchronously
        
        // Act
        let result =
            client.UpdateAuthTokens()
            |> Async.bind (fun () -> client.CreateImage(prompt, variant))
            |> Async.RunSynchronously
        
        // Assert
        match result with
        | Error error -> failwith $"result should be ok, but is error: {error}"
        | Ok result ->
            %result.Files.Should().NotBeEmpty()
            %result.Files.Should().HaveLength(2)
            %result.Files.First().Should().StartWith("task_")
            %result.Files.Skip(1).First().Should().StartWith("task_")
