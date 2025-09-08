namespace FunSharp.DeviantArt.Api.Test

open System.IO
open Newtonsoft.Json
open Xunit
open Faqt
open Faqt.Operators
open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model

[<Trait("Category", "Standard")>]
module ``Model Tests`` =
    
    let tupleShouldBe expectedA expectedB tuple =
        
        %(fst tuple).Should().Be(expectedA)
        %(snd tuple).Should().Be(expectedB)
    
    [<Fact>]
    let ``StashPublication.toProperties() should split arrays`` () =
    
        // Arrange
        let publication = {
            IsMature = true
            Feature = false
            AllowComments = true
            DisplayResolution = DisplayResolution.Original |> int
            LicenseOptions = { CreativeCommons = true; Commercial = true; Modify = "share" }
            Galleries = ["invalid-uuid"] |> Array.ofList
            AllowFreeDownload = true
            AddWatermark = false
            Tags = [ "digital_art"; "made_with_ai" ] |> Array.ofList
            IsAiGenerated = true
            NoAi = false
            ItemId = 123L
        }
        
        // Act
        let result = PublishSubmission.toProperties publication
        
        // Assert
        %result.Should().HaveLength(13)
        
        result[0] |> tupleShouldBe "is_mature" "true"
        result[8] |> tupleShouldBe "tags[]" "digital_art"
        result[9] |> tupleShouldBe "tags[]" "made_with_ai"
