namespace DeviantArt.Api.Test

open System.IO
open DeviantArt.Api
open Newtonsoft.Json
open Xunit
open Faqt
open Faqt.Operators

[<Trait("Category", "OnDemand")>]
module ``DeviantArt Api Tests`` =
    
    type Secrets = {
        client_id: string
        client_secret: string
        valid_persistence: DeviantArt.TokenResponse
    }
    
    let private secretsFilePath = ".secrets"
    
    let private  secrets =
        if File.Exists secretsFilePath then
            File.ReadAllText secretsFilePath
            |> JsonConvert.DeserializeObject<Secrets>
        else
            failwith $"{secretsFilePath} is missing"
    
    type InMemoryPersistence() =
        interface IPersistence<DeviantArt.TokenResponse> with
            member _.Load() =
                Some secrets.valid_persistence
            member _.Save _ =
                ()
        
    [<Fact>]
    let ``WhoAmI returns expected user`` () =
    
        // Arrange
        let persistence = InMemoryPersistence()
        let client = DeviantArt.Client(persistence, secrets.client_id, secrets.client_secret)
        
        // Act
        let result = client.WhoAmI () |> Async.RunSynchronously
        
        // Assert
        %result.username.Should().Be("CarminDez")
