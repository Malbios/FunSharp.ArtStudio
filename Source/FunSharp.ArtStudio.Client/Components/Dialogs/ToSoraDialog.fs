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
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            
            text this.Prompt.Text
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                
                Button.renderSimple "3:2" <| fun () -> this.DialogService.Close(AspectRatio.Landscape)
                Button.renderSimple "1:1" <| fun () -> this.DialogService.Close(AspectRatio.Square)
                Button.renderSimple "2:3" <| fun () -> this.DialogService.Close(AspectRatio.Portrait)
            }
            
            Button.renderSimple "Cancel" <| fun () -> this.DialogService.Close(null)
        }
        
    static member OpenAsync(dialogService: DialogService, prompt: Prompt) =
        
        let parameters = dict [
            "Prompt", box prompt
        ]
        
        let parameters = Dictionary<string, obj>(parameters)
        
        dialogService.OpenAsync<ToSoraDialog>("To Sora Task", parameters)
