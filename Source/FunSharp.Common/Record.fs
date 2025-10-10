namespace FunSharp.Common

open System
open System.Reflection
open FSharp.Reflection
open Newtonsoft.Json

[<RequireQualifiedAccess>]
module Record =
        
    type KeyValueType = {
        Key: string
        Value: string
        Type: Type
    }

    let toMap (record: 'T) =
        FSharpType.GetRecordFields(typeof<'T>)
        |> Array.map (fun property ->
            let value =
                property.GetValue(record) |> JsonConvert.SerializeObject

            let name =
                match property.GetCustomAttribute(typeof<JsonPropertyAttribute>) :?> JsonPropertyAttribute with
                | v when not (isNull v) && not (String.IsNullOrEmpty v.PropertyName) -> v.PropertyName
                | _ -> property.Name

            name, value
        )
        |> Map.ofArray
    
    let toKeyValueTypes (record: 'T) =
        
        FSharpType.GetRecordFields(typeof<'T>)
        |> List.ofArray
        |> List.map (fun property ->
            let name =
                match property.GetCustomAttribute(typeof<JsonPropertyAttribute>) :?> JsonPropertyAttribute with
                | v when not (isNull v) && not (String.IsNullOrEmpty v.PropertyName) -> v.PropertyName
                | _ -> property.Name
                
            let value = property.GetValue(record) |> JsonConvert.SerializeObject

            { Key = name; Value = value; Type = property.PropertyType }
        )

    
[<RequireQualifiedAccess>]
module KeyValueType =
    
    let get valueType key value : Record.KeyValueType = {
        Key = key
        Value = value
        Type = valueType
    }
    
    let splitArrays (values: Record.KeyValueType list) =
        values
        |> Seq.collect (fun kvt ->
            if kvt.Type.IsArray then
                JsonConvert.DeserializeObject<string array>(kvt.Value)
                |> Seq.map (fun v -> $"{kvt.Key}[]", v)
            else
                Seq.singleton (kvt.Key, kvt.Value)
        )
        |> List.ofSeq
