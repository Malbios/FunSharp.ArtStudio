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
    let ``StashPublication.toProperties() should split arrays`` () =
    
        // Arrange
        let publication = {
            IsMature = true
            Feature = false
            AllowComments = true
            DisplayResolution = DisplayResolution.Original |> int
            LicenseOptions = { CreativeCommons = true; Commercial = true; Modify = LicenseOptionsModify.Share }
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
        %result.Should().HaveLength(18)
        
        result[0] |> tupleShouldBe "is_mature" "true"
        result[1] |> tupleShouldBe "feature" "false"
        result[2] |> tupleShouldBe "allow_comments" "true"
        result[3] |> tupleShouldBe "display_resolution" "0"
        result[4] |> tupleShouldBe "license_options[]" "creative_commons"
        result[5] |> tupleShouldBe "creative_commons" "true"
        result[6] |> tupleShouldBe "license_options[]" "commercial"
        result[7] |> tupleShouldBe "commercial" "true"
        result[8] |> tupleShouldBe "license_options[]" "modify"
        result[9] |> tupleShouldBe "modify" "share"
        result[10] |> tupleShouldBe "galleryids[]" "invalid-uuid"
        result[11] |> tupleShouldBe "allow_free_download" "true"
        result[12] |> tupleShouldBe "add_watermark" "false"
        result[13] |> tupleShouldBe "tags[]" "digital_art"
        result[14] |> tupleShouldBe "tags[]" "made_with_ai"
        result[15] |> tupleShouldBe "is_ai_generated" "true"
        result[16] |> tupleShouldBe "noai" "false"
        result[17] |> tupleShouldBe "itemid" "123"
