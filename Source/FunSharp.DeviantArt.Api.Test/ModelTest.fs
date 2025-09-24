namespace FunSharp.DeviantArt.Api.Test

open Xunit
open Faqt
open Faqt.Operators
open FunSharp.DeviantArt.Api.Model

[<Trait("Category", "Standard")>]
module ``Model Tests`` =
    
    let tupleShouldBe expectedA expectedB tuple =
        
        %(fst tuple).Should().Be(expectedA)
        %(snd tuple).Should().Be(expectedB)
    
    [<Fact>]
    let ``toProperties should split arrays`` () =
    
        // Arrange
        let publication : PublishSubmission = {
            itemid = 123L
            is_mature = true
            feature = false
            allow_comments = true
            display_resolution = DisplayResolution.Original |> int
            galleryids = ["invalid-uuid"] |> Array.ofList
            allow_free_download = true
            add_watermark = false
            tags = [ "digital_art"; "made_with_ai" ] |> Array.ofList
            is_ai_generated = true
            noai = false
        }
        
        // Act
        let result = PublishSubmission.toProperties publication
        
        // Assert
        %result.Should().HaveLength(12)
        
        result[0] |> tupleShouldBe "itemid" "123"
        result[1] |> tupleShouldBe "is_mature" "true"
        result[2] |> tupleShouldBe "feature" "false"
        result[3] |> tupleShouldBe "allow_comments" "true"
        result[4] |> tupleShouldBe "display_resolution" "0"
        result[5] |> tupleShouldBe "galleryids[]" "invalid-uuid"
        result[6] |> tupleShouldBe "allow_free_download" "true"
        result[7] |> tupleShouldBe "add_watermark" "false"
        result[8] |> tupleShouldBe "tags[]" "digital_art"
        result[9] |> tupleShouldBe "tags[]" "made_with_ai"
        result[10] |> tupleShouldBe "is_ai_generated" "true"
        result[11] |> tupleShouldBe "noai" "false"
