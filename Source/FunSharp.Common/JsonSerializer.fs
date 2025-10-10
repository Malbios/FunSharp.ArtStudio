namespace FunSharp.Common

open System
open System.Text.Json
open System.Text.Json.Serialization

module JsonSerializer =

    let private options =
        
        let options = JsonSerializerOptions()
        
        options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        options.WriteIndented <- true
        options.PropertyNameCaseInsensitive <- true

        let jsonFsharpOptions =
            JsonFSharpOptions
                .Default()
                .WithUnionExternalTag()
                .WithUnionTagCaseInsensitive()
                .WithUnionTagNamingPolicy(JsonNamingPolicy.CamelCase)
                .WithUnionFieldNamingPolicy(JsonNamingPolicy.CamelCase)
                .WithSkippableOptionFields(SkippableOptionFields.Always)
                .WithUnionUnwrapSingleCaseUnions(true)

        jsonFsharpOptions.AddToJsonSerializerOptions(options)
        
        options

    let serialize value =
        
        JsonSerializer.Serialize(value, options)
        
    let deserialize<'T> (value: string) =
        
        JsonSerializer.Deserialize<'T>(value, options)

    let tryDeserialize<'T> (value: string) =
        
        try
            let value = deserialize<'T> value
            Some value
        with
        | :? JsonException -> None
        | :? NotSupportedException -> None
