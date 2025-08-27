namespace FunSharp.Common

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

        jsonFsharpOptions.AddToJsonSerializerOptions(options)
        
        options

    let serialize value =
        
        JsonSerializer.Serialize(value, options)
        
    let deserialize<'T> (value: string) =
        
        JsonSerializer.Deserialize<'T>(value, options)
