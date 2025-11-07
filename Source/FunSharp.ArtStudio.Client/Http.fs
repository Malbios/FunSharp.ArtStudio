namespace FunSharp.ArtStudio.Client

open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Threading.Tasks
open FunSharp.Common

[<RequireQualifiedAccess>]
module Http =
    
    let private ensureSuccess (task: Task<HttpResponseMessage>) =
        
        task
        |> Async.AwaitTask
        |> Async.bind (fun response ->
            if response.IsSuccessStatusCode then
                response |> Async.returnM
            else
                response.Content.ReadAsStringAsync()
                |> Async.AwaitTask
                |> Async.map failwith
        )
        |> Async.getOrFail
        
    let private asStringContent value =
        
        value
        |> JsonSerializer.serialize
        |> fun x -> new StringContent(x, Encoding.UTF8, "application/json")
        
    let get (client: HttpClient) (url: string) =
        
        client.GetAsync(url)
        |> ensureSuccess
        
    let post (client: HttpClient) (url: string) (content: HttpContent option) =
        
        match content with
        | None -> client.PostAsync(url, null)
        | Some content -> client.PostAsync(url, content)
        |> ensureSuccess
        
    let put (client: HttpClient) (url: string) content =
        
        client.PutAsync(url, content)
        |> ensureSuccess
        
    let patch (client: HttpClient) (url: string) content =
        
        client.PatchAsync(url, content)
        |> ensureSuccess
        
    let delete (client: HttpClient) (url: string) =
        
        client.DeleteAsync(url)
        |> ensureSuccess
        
    let postObject client url value =
        
        value
        |> asStringContent
        |> fun x -> post client url (Some x)
        
    let putObject client url value=
        
        value
        |> asStringContent
        |> put client url
        
    let putString client url (value: string) =
        
        new StringContent(value)
        |> put client url
        
    let putFile client url name mimeType content =
        
        let byteContent = new ByteArrayContent(content)
        byteContent.Headers.ContentType <- MediaTypeHeaderValue(mimeType)

        use content = new MultipartFormDataContent()
        content.Add(byteContent, "file", name)
        
        content
        |> put client url
        
    let patchObject client url value =
        
        value
        |> asStringContent
        |> patch client url
        
    let contentAsString (response: HttpResponseMessage) =
        
        response.Content.ReadAsStringAsync()
        |> Async.AwaitTask
        |> Async.getOrFail
