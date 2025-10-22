namespace FunSharp.ArtStudio.Client.Components

open Bolero
open FunSharp.ArtStudio.Model
open FunSharp.Blazor.Components
open Microsoft.JSInterop

[<RequireQualifiedAccess>]
module CopyPrompt =
    
    let render (origin: DeviationOrigin) =
        
        origin
        |> DeviationOrigin.prompt
        |> function
            | None -> Node.Empty()
            | Some prompt ->
                fun (js: IJSRuntime) ->
                    Button.renderSimple "Copy Prompt" <| fun () -> Helpers.copyToClipboard js prompt.Text
                |> Injector.withInjected<IJSRuntime>
