namespace FunSharp.DeviantArt.Manager.Components

open System.Threading.Tasks
open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Microsoft.FSharp.Reflection
open FunSharp.Blazor.Components
open Radzen
open Radzen.Blazor

type ItemEditor<'T when 'T : not struct and 'T : equality>() =
    inherit Component()
    
    let mutable shouldRender = false
    
    [<Parameter>]
    member val Fields : Map<string, (obj -> unit) -> obj -> Node> = Map.empty with get, set
    
    [<Parameter>]
    member val Item : 'T option = None with get, set

    [<Parameter>]
    member val SaveLabel = "Save" with get, set

    [<Parameter>]
    member val OnSave : 'T -> unit = ignore with get, set

    [<Parameter>]
    member val FinishLabel = "Finish" with get, set

    [<Parameter>]
    member val OnFinish : 'T -> unit = ignore with get, set

    [<Parameter>]
    member val ForgetLabel = "Forget" with get, set

    [<Parameter>]
    member val OnForget : 'T -> unit = ignore with get, set
    
    member private this.Save() =
        match this.Item with
        | None -> ()
        | Some v -> this.OnSave v
        
        // shouldRender <- true
        // this.StateHasChanged()
        
    member private this.Finish() =
        match this.Item with
        | None -> ()
        | Some v -> this.OnFinish v
        
        // shouldRender <- true
        // this.StateHasChanged()
        
    member private this.Forget() =
        match this.Item with
        | None -> ()
        | Some v -> this.OnForget v
        
        // shouldRender <- true
        // this.StateHasChanged()
    
    member private this.Update(newItem: 'T) =
        
        this.Item <- newItem |> Some
        
        // shouldRender <- true
        // this.StateHasChanged()
        
    member private this.RenderFieldEditor(fieldName: string, fieldValue: obj, withChange: obj -> 'T) =
        
        match this.Fields.TryFind(fieldName) with
        | None ->
            Node.Empty ()
        | Some renderAction ->
            let onChange (newValue: obj) = this.Update(withChange newValue)
            renderAction onChange fieldValue
            
    member private this.TriggerRender() =
        
        shouldRender <- true
        this.StateHasChanged()
            
    override this.SetParametersAsync(parameters: ParameterView) =
        let prevItem = this.Item
        let tcs = TaskCompletionSource<unit>()

        let task = base.SetParametersAsync(parameters)
        task.ContinueWith(fun (t: Task) ->
            if t.IsFaulted then
                tcs.SetException(t.Exception.InnerExceptions)
            elif t.IsCanceled then
                tcs.SetCanceled()
            else
                if this.Item <> prevItem then
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
        
        match this.Item with
        | None ->
            LoadingWidget.render ()
        | Some item ->
            let recordType = typeof<'T>
            
            match FSharpType.IsRecord(recordType, true) with
            | false ->
                failwith $"Not a record type: {recordType.FullName}"
            | true ->
                let fields = FSharpType.GetRecordFields(recordType, true)
                let values = FSharpValue.GetRecordFields(item, true)
                
                comp<RadzenStack> {
                    "Orientation" => Orientation.Vertical
                    
                    [
                        for i in 0 .. fields.Length - 1 do
                            let field = fields[i]
                            let value = values[i]
                            
                            let withChange newValue =
                                let updatedValues = Array.copy values
                                updatedValues[i] <- newValue
                                FSharpValue.MakeRecord(recordType, updatedValues, true) :?> 'T
                                
                            yield this.RenderFieldEditor(field.Name, value, withChange)
                    ]
                    |> Helpers.renderList
                    
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Horizontal
                        "JustifyContent" => JustifyContent.Center
                        "AlignItems" => AlignItems.Center

                        Button.render this this.Forget this.ForgetLabel
                        Button.render this this.Save this.SaveLabel
                        Button.render this this.Finish this.FinishLabel
                    }
                }
                
    // let onChange newValue = this.Update(withChange newValue)
    // TextInput.render onChange (fun _ -> ()) $"Enter {fieldName}..." (fieldValue :?> string)
    
    // cond fieldType.FullName <| function
    // | "System.String" ->
    //     input {
    //         attr.value (fieldValue :?> string)
    //         on.change (fun ev -> onChange (ev.Value :> obj))
    //     }
    // | "System.Int64" ->
    //     input {
    //         attr.value (string (fieldValue :?> int64))
    //         attr.``type`` "number"
    //         on.change (fun ev -> onChange (Int64.Parse ev.Value :> obj))
    //     }
    // | "System.Boolean" ->
    //     input {
    //         attr.``type`` "checkbox"
    //         attr.``checked`` (fieldValue :?> bool)
    //         on.change (fun ev -> onChange (box (ev.Value)))
    //     }
    // | "System.Uri" ->
    //     input {
    //         attr.value ((fieldValue :?> Uri).ToString())
    //         on.change (fun ev -> onChange (Uri(ev.Value) :> obj))
    //     }
    // | _ ->
    //     div { text $"Unsupported field: {fieldName}" }
