namespace FunSharp.DeviantArt.Manager.Components

open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Api.Model

type LocalDeviations() =
    inherit Component()
    
    [<Parameter>]
    member val Galleries = Array.empty<string> with get, set
    
    [<Parameter>]
    member val Items : Loadable<LocalDeviation array> = NotLoaded with get, set
    
    [<Parameter>]
    member val OnSave : LocalDeviation -> unit = ignore with get, set
    
    [<Parameter>]
    member val OnStash : LocalDeviation -> unit = ignore with get, set
    
    [<Parameter>]
    member val OnForget : LocalDeviation -> unit = ignore with get, set
    
    override this.Render() =
        
        Loadable.render this.Items
        <| fun deviations ->
            Deviations.render
            <| concat {
                for deviation in deviations do
                    comp<LocalDeviationEditor> {
                        "Galleries" => this.Galleries
                        "Deviation" => Some deviation
                        "OnSave" => this.OnSave
                        "OnStash" => this.OnStash
                        "OnForget" => this.OnForget
                    }
            }
