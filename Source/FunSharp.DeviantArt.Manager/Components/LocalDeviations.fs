namespace FunSharp.DeviantArt.Manager.Components

open System.Threading.Tasks
open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Api.Model
open Radzen
open Radzen.Blazor

type LocalDeviations() =
    inherit Component()
    
    let mutable shouldRender = false
    
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
    
    member private this.Save(item: LocalDeviation) =
        
        this.OnSave(item)
        
        // shouldRender <- true
        // this.StateHasChanged()
        
    member private this.Stash(item: LocalDeviation) =
        
        this.OnStash(item)
        
        // shouldRender <- true
        // this.StateHasChanged()
        
    member private this.Forget(item: LocalDeviation) =
        
        this.OnForget(item)
        
        // shouldRender <- true
        // this.StateHasChanged()
    
    member private this.FieldConfigurations() =
        
        let metadata (onChange: obj -> unit) (currentValue: obj) =
            let currentMetadata = currentValue :?> Metadata
            
            let onNewTitle (newTitle: string) =
                { currentMetadata with Title = newTitle } |> onChange
                
            let onNewGallery (newGallery: string) =
                { currentMetadata with Gallery = newGallery } |> onChange
                
            let onNewIsMature (newIsMature: bool) =
                { currentMetadata with IsMature = newIsMature } |> onChange
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Vertical
                
                TextInput.render onNewTitle (fun _ -> ()) "Enter title..." currentMetadata.Title
                
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    
                    DropDown.render onNewGallery "Gallery" "Select gallery..." this.Galleries currentMetadata.Gallery
                    
                    CheckBox.render onNewIsMature "IsMature" currentMetadata.IsMature
                }
            }
            
        [
            ("Metadata", metadata)
        ]
        |> Map.ofList
            
    member private this.TriggerRender() =
        
        printfn "triggering local deviations render"
        shouldRender <- true
        this.StateHasChanged()
            
    override this.SetParametersAsync(parameters: ParameterView) =
        let tcs = TaskCompletionSource<unit>()
        let task = base.SetParametersAsync(parameters)
        
        task.ContinueWith(fun (t: Task) ->
            if t.IsFaulted then
                tcs.SetException(t.Exception.InnerExceptions)
            elif t.IsCanceled then
                tcs.SetCanceled()
            else
                // Just always render when parameters are set
                this.TriggerRender()
                tcs.SetResult(())
        ) |> ignore

        tcs.Task
        
    override this.ShouldRender() =
        
        if shouldRender then
            shouldRender <- false
            true
        else
            
        base.ShouldRender()
    
    override this.Render() =
        
        printfn "rendering local deviations"
        
        Loadable.render this.Items
        <| fun deviations ->
            Deviations.render
            <| concat {
                for deviation in deviations do
                        
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Vertical
                        "JustifyContent" => JustifyContent.Left
                        
                        ImageUrl.render (Some deviation.ImageUrl)
                        
                        match deviation.Origin with
                        | DeviationOrigin.None -> ()
                        | DeviationOrigin.Inspiration inspiration ->
                            Link.render None inspiration.Url
                        | DeviationOrigin.Prompt prompt ->
                            match prompt.Inspiration with
                            | None -> ()
                            | Some inspiration ->
                                Link.render None inspiration.Url

                        comp<ItemEditor<LocalDeviation>> {
                            "Fields" => this.FieldConfigurations()
                            "Item" => Some deviation
                            "OnSave" => this.Save
                            "FinishLabel" => "Stash"
                            "OnFinish" => this.Stash
                            "OnForget" => this.Forget
                        }
                    }
                    |> Deviation.renderWithContent (Some deviation.ImageUrl)
                    
                    comp<LocalDeviationEditor> {
                        "Galleries" => this.Galleries
                        "Deviation" => Some deviation
                        "OnSave" => this.OnSave
                        "OnStash" => this.OnStash
                        "OnForget" => this.OnForget
                    }
            }
