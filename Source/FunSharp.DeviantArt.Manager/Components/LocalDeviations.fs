namespace FunSharp.DeviantArt.Manager.Components

open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager.Model

type LocalDeviations() =
    inherit Component()

    [<Parameter>]
    member val LoadImage : string -> unit = ignore with get, set
    
    [<Parameter>]
    member val Galleries = Array.empty<string> with get, set
    
    [<Parameter>]
    member val Images: Map<string, Loadable<Image>> = Map.empty with get, set
    
    [<Parameter>]
    member val Items : Loadable<LocalDeviation array> = Loadable.NotLoaded with get, set

    [<Parameter>]
    member val OnSave : LocalDeviation -> unit = ignore with get, set

    [<Parameter>]
    member val OnStash : LocalDeviation -> unit = ignore with get, set
        
    override this.Render() =
        
        match this.Items with
        | Loaded deviations ->
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "JustifyContent" => JustifyContent.Center
                "AlignItems" => AlignItems.Center
                "Wrap" => FlexWrap.Wrap
                
                for deviation in deviations do
                    match this.Images |> Map.tryFind deviation.Id with
                    | None -> this.LoadImage deviation.Id
                    | _ -> ()
                    
                    comp<LocalDeviationEditor> {
                        "Galleries" => this.Galleries
                        "Deviation" => deviation
                        "Image" => (this.Images |> Map.tryFind deviation.Id)
                        "OnSave" => this.OnSave
                        "OnStash" => this.OnStash
                    }
            }
            
        | _ ->
            LoadingWidget.render ()
