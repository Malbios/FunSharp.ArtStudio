namespace FunSharp.DeviantArt.Manager.Components

open System
open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Microsoft.FSharp.Reflection
open FunSharp.Blazor.Components

module ItemEditor =
            
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

    type ItemEditor<'T when 'T : not struct>() =
        inherit Component()
        
        [<Parameter>]
        member val Item : 'T option = None with get, set

        [<Parameter>]
        member val OnSave : 'T -> unit = ignore with get, set

        [<Parameter>]
        member val OnFinish : 'T -> unit = ignore with get, set

        [<Parameter>]
        member val OnForget : 'T -> unit = ignore with get, set
        
        member private this.Update(newItem: 'T) =
            
            this.Item <- newItem |> Some
            
        member private this.RenderFieldEditor(fieldName: string, fieldType: Type, fieldValue: obj, withChange: obj -> 'T) =
            
            cond fieldType.FullName <| function
            | "System.String" ->
                let onChange newValue = this.Update(withChange newValue)
                TextInput.render onChange (fun _ -> ()) $"Enter {fieldName}..." (fieldValue :?> string)
            | _ ->
                div { text $"Unsupported field: {fieldName}" }
        
        override this.Render() =
            
            match this.Item with
            | None ->
                LoadingWidget.render ()
            | Some item ->
                let recordType = typeof<'T>
                
                match FSharpType.IsRecord(recordType, true) with
                | false ->
                    div { text "Not a record type." }
                | true ->
                    let fields = FSharpType.GetRecordFields(recordType, true)
                    let values = FSharpValue.GetRecordFields(item, true)
                    
                    div {
                        for i in 0 .. fields.Length - 1 do
                            let field = fields[i]
                            let value = values[i]
                            
                            let withChange newValue =
                                let updatedValues = Array.copy values
                                updatedValues[i] <- newValue
                                FSharpValue.MakeRecord(recordType, updatedValues, true) :?> 'T
                                
                            yield
                                div {
                                    label { text field.Name }
                                    this.RenderFieldEditor(field.Name, field.PropertyType, value, withChange)
                                }
                    }
