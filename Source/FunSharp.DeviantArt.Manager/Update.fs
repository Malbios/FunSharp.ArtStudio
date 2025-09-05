namespace FunSharp.DeviantArt.Manager

open System.IO
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Threading.Tasks
open Elmish
open FunSharp.Common
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager.Model
open Microsoft.AspNetCore.Components.Forms

module Update =
    
    let private apiRoot = "http://localhost:5123/api/v1"
    
    let private ensureSuccess (task: Task<HttpResponseMessage>) =
        
        task
        |> Async.AwaitTask
        |> Async.tee (fun response -> response.EnsureSuccessStatusCode() |> ignore)
        |> Async.catch
        |> AsyncResult.getOrFail
    
    let private get (client: HttpClient) (url: string) =
        
        client.GetAsync(url)
        |> ensureSuccess
        
    let private post (client: HttpClient) (url: string) content =
        
        client.PostAsync(url, content)
        |> ensureSuccess
        
    let private patch (client: HttpClient) (url: string) content =
        
        client.PatchAsync(url, content)
        |> ensureSuccess
        
    let private contentAsString (response: HttpResponseMessage) =
        
        response.Content.ReadAsStringAsync()
        |> Async.AwaitTask
        |> Async.catch
        |> AsyncResult.getOrFail
        
    let private postObject client url object =
        
        object
        |> JsonSerializer.serialize
        |> fun x -> new StringContent(x, Encoding.UTF8, "application/json")
        |> post client url
        
    let private patchObject client url object =
        
        object
        |> JsonSerializer.serialize
        |> fun x -> new StringContent(x, Encoding.UTF8, "application/json")
        |> patch client url
        
    let private postFile client url name mimeType content=
        
        let byteContent = new ByteArrayContent(content)
        
        byteContent.Headers.ContentType <- MediaTypeHeaderValue(mimeType)
        
        use content = new MultipartFormDataContent()
        
        content.Add(byteContent, "file", name)
        // content.Add(new StringContent(deviation.Title), "title")
        
        content
        |> post client url
        
    let private processStashSubmission (deviation: LocalDeviation) content =
        
        let stashSubmission = content |> JsonSerializer.deserialize<StashSubmissionResponse>
            
        match stashSubmission.status with
        | "success" ->
            let submission = {
                StashId = stashSubmission.item_id
                Metadata = deviation
            }
            
            (deviation, submission)
        | _ ->
            failwith $"Failed to stash {deviation.Image.Name}"
    
    let private loadItems<'T> client endpoint (asLoadable: string -> Loadable<'T>) =
        
        $"{apiRoot}{endpoint}"
        |> get client
        |> Async.bind contentAsString
        |> Async.map asLoadable
        
    let private loadSettings (client: HttpClient) =
        
        (JsonSerializer.deserialize<Settings> >> Loadable.Loaded)
        |> loadItems client "/settings"
    
    let private loadInspirations client =
        
        (JsonSerializer.deserialize<Inspiration array> >> Loadable.Loaded)
        |> loadItems client "/local/inspirations"
    
    let private loadPrompts client =
        
        (JsonSerializer.deserialize<Prompt array> >> Loadable.Loaded)
        |> loadItems client "/local/prompts"
    
    let private loadLocalDeviations client =
        
        (JsonSerializer.deserialize<LocalDeviation array> >> Loadable.Loaded)
        |> loadItems client "/local/deviations"
    
    let private loadStashedDeviations client =
        
        (JsonSerializer.deserialize<StashedDeviation array> >> Loadable.Loaded)
        |> loadItems client "/stash"
    
    let private loadPublishedDeviations client =
        
        (JsonSerializer.deserialize<PublishedDeviation array> >> Loadable.Loaded)
        |> loadItems client "/publish"
        
    let private processUpload (file: IBrowserFile) = async {
        let maxSize = 1024L * 1024L * 100L // 100 MB
        
        use stream = file.OpenReadStream(maxAllowedSize = maxSize)
        use ms = new MemoryStream()
        
        do! stream.CopyToAsync(ms)
        
        let byteArray = ms.ToArray()
        
        return {
            LocalDeviation.empty with
                Image = Image(file.Name, file.ContentType, byteArray)
        }
    }
    
    let private uploadLocalDeviation client (deviation: LocalDeviation) =
        
        printfn $"uploading {deviation.Image.Name}..."
        
        postFile client $"{apiRoot}/local/deviation" deviation.Image.Name deviation.Image.MimeType deviation.Image.Content
    
    let private updateLocalDeviation client (deviation: LocalDeviation) =
        
        patchObject client $"{apiRoot}/local/deviation" deviation
        |> Async.ignore
    
    let update _ client message model =
    
        match message with
        
        | SetPage page ->
            { model with Page = page }, Cmd.none

        | Initialize ->
            
            let batch = Cmd.batch [
                Cmd.ofMsg LoadSettings
                Cmd.ofMsg LoadInspirations
                Cmd.ofMsg LoadPrompts
                Cmd.ofMsg LoadLocalDeviations
                Cmd.ofMsg LoadStashedDeviations
                Cmd.ofMsg LoadPublishedDeviations
            ]
            
            model, batch

        | LoadSettings ->
            let load () = loadSettings client
            let failed ex = LoadedSettings (Loadable.LoadingFailed ex)
            
            { model with Settings = Loading }, Cmd.OfAsync.either load () LoadedSettings failed
        
        | LoadedSettings loadable ->
           { model with Settings = loadable }, Cmd.none
        
        | LoadInspirations ->
            
            let load () = loadInspirations client
            let failed ex = LoadedInspirations (Loadable.LoadingFailed ex)
            
            { model with Inspirations = Loading }, Cmd.OfAsync.either load () LoadedInspirations failed
            
        | LoadedInspirations loadable ->
            { model with Inspirations = loadable }, Cmd.none

        | LoadPrompts ->
            
            let load () = loadPrompts client
            let failed ex = LoadedPrompts (Loadable.LoadingFailed ex)
            
            { model with Prompts = Loading }, Cmd.OfAsync.either load () LoadedPrompts failed

        | LoadedPrompts loadable ->
            { model with Prompts = loadable }, Cmd.none

        | LoadLocalDeviations ->
            
            let load () = loadLocalDeviations client
            let failed ex = LoadedLocalDeviations (Loadable.LoadingFailed ex)
            
            { model with LocalDeviations = Loading }, Cmd.OfAsync.either load () LoadedLocalDeviations failed

        | LoadedLocalDeviations loadable ->
            { model with LocalDeviations = loadable }, Cmd.none

        | LoadStashedDeviations ->
            
            let load () = loadStashedDeviations client
            let failed ex = LoadedStashedDeviations (Loadable.LoadingFailed ex)
            
            { model with StashedDeviations = Loading }, Cmd.OfAsync.either load () LoadedStashedDeviations failed

        | LoadedStashedDeviations loadable ->
            { model with StashedDeviations = loadable }, Cmd.none

        | LoadPublishedDeviations ->
            let load () = loadPublishedDeviations client
            let failed ex = LoadedPublishedDeviations (Loadable.LoadingFailed ex)
            
            { model with PublishedDeviations = Loading }, Cmd.OfAsync.either load () LoadedPublishedDeviations failed

        | LoadedPublishedDeviations loadable ->
            { model with PublishedDeviations = loadable }, Cmd.none

        | AddInspiration inspiration ->
            failwith "todo"
            
        | AddInspirationFailed (error, inspiration) ->
            failwith "todo"
        
        | Inspiration2Prompt (inspiration, prompt) ->
            failwith "todo"
            
        | Inspiration2PromptFailed (error, inspiration, prompt) ->
            failwith "todo"
        
        | Prompt2LocalDeviation (prompt, local) ->
            failwith "todo"
            
        | Prompt2LocalDeviationFailed (error, prompt, local) ->
            failwith "todo"
        
        | StashDeviation deviation ->
            failwith "todo"
            
        | StashedDeviation (local, stashed) ->
            failwith "todo"
            
        | StashDeviationFailed (error, local) ->
            failwith "todo"
        
        | PublishStashed stashedDeviation ->
            failwith "todo"
            
        | PublishedDeviation (stashed, published) ->
            failwith "todo"
            
        | PublishStashedFailed (error, stashed) ->
            failwith "todo"
            
        | ProcessImages newFiles ->
            
            let error ex file = ProcessImageFailed (ex, file)
            
            let cmd file =
                Cmd.OfAsync.either processUpload file ProcessedImage (fun x -> error x file)
            
            model, Cmd.batch (newFiles |> Array.map cmd)

        | ProcessedImage deviation ->
            model, Cmd.ofMsg (UploadLocalDeviation deviation)

        | ProcessImageFailed (error, file) ->
            printfn $"processing failed for: {file |> JsonSerializer.serialize}"
            printfn $"error: {error}"
            
            model, Cmd.none
        
        | UploadLocalDeviation deviation ->
            
            let upload = uploadLocalDeviation client
            let success _ = LoadLocalDeviations
            let error ex = UploadLocalDeviationFailed (ex, deviation)
            
            model, Cmd.OfAsync.either upload deviation success error

        | UploadLocalDeviationFailed (error, deviation) ->
            failwith "todo"

        | UpdateLocalDeviation deviation ->
            
            let update = updateLocalDeviation client
            let success _ = LoadLocalDeviations
            let error ex = UpdateLocalDeviationFailed (ex, deviation)
            
            model, Cmd.OfAsync.either update deviation success error

        | UpdateLocalDeviationFailed (error, local) ->
            failwith "todo"
