namespace FunSharp.Common

open System
open System.Reflection
open FSharp.Reflection
open Newtonsoft.Json

[<RequireQualifiedAccess>]
module Record =

    let toMap (record: 'T) : Map<string, string> =
        FSharpType.GetRecordFields(typeof<'T>)
        |> Array.map (fun property ->
            let value =
                property.GetValue(record) |> string

            let name =
                match property.GetCustomAttribute(typeof<JsonPropertyAttribute>) :?> JsonPropertyAttribute with
                | v when not (isNull v) && not (String.IsNullOrEmpty v.PropertyName) -> v.PropertyName
                | _ -> property.Name

            name, value
        )
        |> Map.ofArray
