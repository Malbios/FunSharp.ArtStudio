namespace FunSharp.DeviantArt.Manager.Components

open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Radzen
open Radzen.Blazor
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager.Model

type LocalDeviationsEditor() =
    inherit Component()
    
    let mutable draft = Array.empty<LocalDeviation>
    
    [<Parameter>]
    member val Galleries = Array.empty<string> with get, set
    
    [<Parameter>]
    member val Items : Loadable<LocalDeviation array> = Loadable.NotLoaded with get, set

    [<Parameter>]
    member val OnSave : LocalDeviation -> unit = ignore with get, set

    [<Parameter>]
    member val OnStash : LocalDeviation -> unit = ignore with get, set
    
    override this.OnParametersSet() =
        
        let deviations =
            match this.Items with
            | Loaded v -> v
            | _ -> Array.empty
        
        draft <- deviations
        
    override this.Render() =
        
        match this.Items with
        | Loaded _ ->
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "JustifyContent" => JustifyContent.Center
                "AlignItems" => AlignItems.Center
                "Wrap" => FlexWrap.Wrap
                
                for deviation in draft do
                    comp<LocalDeviationEditor> {
                        "Item" => deviation
                        "Galleries" => this.Galleries
                        "OnSave" => this.OnSave
                        "OnStash" => this.OnStash
                    }
            }
            
        | NotLoaded
        | Loading ->
            LoadingWidget.render ()
        
        | _ -> Node.Empty ()
