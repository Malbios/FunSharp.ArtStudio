namespace FunSharp.ArtStudio.Client.Components

open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Microsoft.FSharp.Reflection
open FunSharp.Blazor.Components
open Radzen
open Radzen.Blazor

type ItemEditor<'T when 'T : not struct and 'T : equality>() =
    inherit Component()
    
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
        
    member private this.Finish() =
        match this.Item with
        | None -> ()
        | Some v -> this.OnFinish v
        
    member private this.Forget() =
        match this.Item with
        | None -> ()
        | Some v -> this.OnForget v
    
    member private this.Update(newItem: 'T) =
        
        this.Item <- newItem |> Some
        
    member private this.RenderFieldEditor(fieldName: string, fieldValue: obj, withChange: obj -> 'T) =
        
        match this.Fields.TryFind(fieldName) with
        | None ->
            Node.Empty()
        | Some renderAction ->
            let onChange (newValue: obj) = this.Update(withChange newValue)
            renderAction onChange fieldValue
    
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

                        Button.render this.ForgetLabel this.Forget false
                        Button.render this.SaveLabel this.Save false
                        Button.render this.FinishLabel this.Finish false
                    }
                }
