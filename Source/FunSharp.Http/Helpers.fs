namespace FunSharp.Http

open System.Collections.Generic
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open FunSharp.Common

[<RequireQualifiedAccess>]
module Helpers =
    
    let toHttpRequestMessage (payload: RequestPayload) =
        
        match payload with
        | RequestPayload.Get url ->
            new HttpRequestMessage(HttpMethod.Get, url)
            
        | RequestPayload.PostJson (url, json) ->
            let content = new StringContent(json, Encoding.UTF8, "application/json")
            new HttpRequestMessage(HttpMethod.Post, url, Content = content)
            
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
                | InMemory (_, mediaType) -> mediaType
                | Stream (_, mediaType) -> mediaType
                |> Option.defaultValue "application/octet-stream"
                
            let fileContent =
                match file with
                | InMemory (content, _) -> new ByteArrayContent(content) :> HttpContent
                | Stream (content, _) -> new StreamContent(content) :> HttpContent
                
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
