namespace FunSharp.Common

open System
open System.Text.Json
open System.Text.Json.Serialization

module JsonSerializer =
    
    type CaseInsensitiveEnumConverter<'T when 'T : enum<int>>() =
        inherit JsonConverter<'T>()
        
        override _.Read(reader, _, _) =
            
            let value = reader.GetString()
            
            if String.IsNullOrEmpty(value) then
                raise (JsonException "Enum value cannot be null or empty.")
            else
                try
                    Enum.Parse(typeof<'T>, value, ignoreCase = true) :?> 'T
                with _ ->
                    raise (JsonException($"Invalid enum value '{value}' for {typeof<'T>.Name}."))
                    
        override _.Write(writer, value, _) =
            
            writer.WriteStringValue(value.ToString())
            
    type NullTolerantFloatConverter() =
        inherit JsonConverter<float>()
        
        override _.Read(reader, _, _) =
            
            if reader.TokenType = JsonTokenType.Null then 0.0
            else reader.GetDouble()
            
        override _.Write(writer, value, _) =
            
            writer.WriteNumberValue(value)

    let private options (customizer: (JsonSerializerOptions -> JsonSerializerOptions) option) =
        
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
        
        let options =
            match customizer with
            | Some customizer -> customizer options
            | None -> options
        
        options

    let serialize value =
        
        JsonSerializer.Serialize(value, options None)
        
    let deserialize<'T> (value: string) =
        
        JsonSerializer.Deserialize<'T>(value, options None)
        
    let customDeserialize<'T> optionsCustomizer (value: string) =
        
        JsonSerializer.Deserialize<'T>(value, optionsCustomizer |> Some |> options)

    let tryDeserialize<'T> (value: string) =
        
        try
            let value = deserialize<'T> value
            Some value
        with
        | :? JsonException -> None
        | :? NotSupportedException -> None

    let tryCustomDeserialize<'T> optionsCustomizer (value: string) =
        
        try
            let value = customDeserialize<'T> optionsCustomizer value
            Some value
        with
        | :? JsonException as ex ->
            // printfn $"tryCustomDeserialize JsonException:\n{ex.Message}"
            None
        | :? NotSupportedException as ex ->
            // printfn $"tryCustomDeserialize NotSupportedException:\n{ex.Message}"
            None
