namespace FunSharp.ArtStudio.Client.Components

open System.Collections.Generic
open Microsoft.AspNetCore.Components
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
open FunSharp.Blazor.Components
open FunSharp.OpenAI.Api.Model.Sora
open FunSharp.ArtStudio.Model

type ToSoraDialog() =
    inherit Component()

    [<Inject>]
    member val DialogService = Unchecked.defaultof<DialogService> with get, set
    
    [<Parameter>]
    member val Prompt : Prompt = Prompt.empty with get, set

    override this.Render() =
        
        let inspirationImageUrl = this.Prompt.Inspiration |> Option.bind _.ImageUrl
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            "JustifyContent" => JustifyContent.Center
            "AlignItems" => AlignItems.Center
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Vertical
                "JustifyContent" => JustifyContent.Center
                "AlignItems" => AlignItems.Center
                "Gap" => "0.2rem"
                
                comp<Image> {
                    "ImageUrl" => inspirationImageUrl
                    "ClickUrl" => inspirationImageUrl
                }
                
                match this.Prompt.Inspiration with
                | Some inspiration ->
                    Link.renderSimple (Some "DA Link") inspiration.Url
                | None -> ()
            }
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Vertical
                "JustifyContent" => JustifyContent.Center
                "AlignItems" => AlignItems.Center
                
                Button.renderSimple "Landscape (3:2)" <| fun () -> this.DialogService.Close(AspectRatio.Landscape)
                Button.renderSimple "Square (1:1)" <| fun () -> this.DialogService.Close(AspectRatio.Square)
                Button.renderSimple "Portrait (2:3)" <| fun () -> this.DialogService.Close(AspectRatio.Portrait)
            }
        }
        
    static member OpenAsync(dialogService: DialogService, prompt: Prompt) =
        
        let parameters = dict [
            "Prompt", box prompt
        ]
        
        let parameters = Dictionary<string, obj>(parameters)
        
        dialogService.OpenAsync<ToSoraDialog>("To Sora Task", parameters)
