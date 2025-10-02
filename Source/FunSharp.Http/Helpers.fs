namespace FunSharp.Http

open System.Collections.Generic
open System.Net.Http
open System.Net.Http.Headers
open FunSharp.Common

[<RequireQualifiedAccess>]
module Helpers =
    
    let toHttpRequestMessage (payload: RequestPayload) =
        
        match payload with
        | RequestPayload.Get url ->
            new HttpRequestMessage(HttpMethod.Get, url)
            
        | RequestPayload.PostForm (url, properties) ->
            let properties = properties |> Seq.map (fun (k, v) -> KeyValuePair(k, v))
            let content = new FormUrlEncodedContent(properties)
            new HttpRequestMessage(HttpMethod.Post, url, Content = content)
            
        | RequestPayload.PostMultipart (url, file, properties) ->
            let content = new MultipartFormDataContent()
            
            let title =
                properties
                |> List.tryFind (fun (k, _) -> k = "title")
                |> Option.map snd
                |> Option.defaultValue ""
                
            let mediaType =
                match file with
                | InMemory f -> f.MediaType
                | Stream f   -> f.MediaType
                |> Option.defaultValue "application/octet-stream"
                
            let fileContent =
                match file with
                | InMemory f -> new ByteArrayContent(f.Content) :> HttpContent
                | Stream f   -> new StreamContent(f.Content)   :> HttpContent
                
            fileContent.Headers.ContentType <- MediaTypeHeaderValue.Parse(mediaType)
            content.Add(fileContent, "file", title)
            
            for k, v in properties do
                content.Add(new StringContent(v), k)
                
            new HttpRequestMessage(HttpMethod.Post, url, Content = content)
            
    let ensureSuccess (response: HttpResponseMessage) =
        
        if response.IsSuccessStatusCode then
            response |> Async.returnM
        else
            response.Content.ReadAsStringAsync()
            |> Async.AwaitTask
            |> Async.map failwith
        
    let responseContentAsString (response: HttpResponseMessage) =
        
        response.Content.ReadAsStringAsync()
        |> Async.AwaitTask
    
    let toRecord<'T> (response: HttpResponseMessage) =
        response
        |> responseContentAsString
        |> Async.map JsonSerializer.deserialize<'T>
