namespace FunSharp.DeviantArt.Manager

open System.Net.Http
open System.Net.Http.Headers
open System.Threading.Tasks
open Elmish
open FunSharp.Common
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager.Model

module Update =
    
    let private apiRoot = "http://localhost:5123/api/v1"
    
    let private ensureSuccessOrFail (task: Task<HttpResponseMessage>) =
        
        task
        |> Async.AwaitTask
        |> Async.tee (fun response -> response.EnsureSuccessStatusCode() |> ignore)
        |> Async.catch
        |> AsyncResult.getOrFail
    
    let private get (client: HttpClient) (url: string) =
        
        client.GetAsync(url)
        |> ensureSuccessOrFail
        
    let private post (client: HttpClient) (url: string) (content: HttpContent) =
        
        client.PostAsync(url, content)
        |> ensureSuccessOrFail
        
    let private contentAsString (response: HttpResponseMessage) =
        
        response.Content.ReadAsStringAsync()
        |> Async.AwaitTask
        |> Async.catch
        |> AsyncResult.getOrFail
        
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
        
    let private postImage client url (deviation: LocalDeviation) =
        
        let byteContent = new ByteArrayContent(deviation.Image.Content)
        
        byteContent.Headers.ContentType <- MediaTypeHeaderValue(deviation.Image.MimeType)
        
        use content = new MultipartFormDataContent()
        
        content.Add(byteContent, "file", deviation.Image.Name)
        content.Add(new StringContent(deviation.Title), "title")
        
        content
        |> post client url
        |> Async.bind contentAsString
        |> Async.map (processStashSubmission deviation)
    
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
        |> loadItems client "/inspirations"
    
    let private loadPrompts client =
        
        (JsonSerializer.deserialize<Prompt array> >> Loadable.Loaded)
        |> loadItems client "/prompts"
    
    let private loadLocalDeviations client =
        
        (JsonSerializer.deserialize<LocalDeviation array> >> Loadable.Loaded)
        |> loadItems client "/local-deviations"
    
    let private loadStashedDeviations client =
        
        (JsonSerializer.deserialize<StashedDeviation array> >> Loadable.Loaded)
        |> loadItems client "/stashed-deviations"
    
    let private loadPublishedDeviations client =
        
        (JsonSerializer.deserialize<PublishedDeviation array> >> Loadable.Loaded)
        |> loadItems client "/published-deviations"
    
    let update _ client message model =
    
        match message with
        
        | SetPage page ->
            { model with Page = page }, Cmd.none

        | Initialize ->
            
            let batch = Cmd.batch [
                Cmd.ofMsg LoadSettings
                Cmd.ofMsg LoadInspirations
                Cmd.ofMsg LoadPrompts
                Cmd.ofMsg LoadLocalDeviation
                Cmd.ofMsg LoadStashedDeviation
                Cmd.ofMsg LoadPublishedDeviation
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

        | LoadLocalDeviation ->
            
            let load () = loadLocalDeviations client
            let failed ex = LoadedLocalDeviation (Loadable.LoadingFailed ex)
            
            { model with LocalDeviations = Loading }, Cmd.OfAsync.either load () LoadedLocalDeviation failed

        | LoadedLocalDeviation loadable ->
            { model with LocalDeviations = loadable }, Cmd.none

        | LoadStashedDeviation ->
            
            let load () = loadStashedDeviations client
            let failed ex = LoadedStashedDeviation (Loadable.LoadingFailed ex)
            
            { model with StashedDeviations = Loading }, Cmd.OfAsync.either load () LoadedStashedDeviation failed

        | LoadedStashedDeviation loadable ->
            { model with StashedDeviations = loadable }, Cmd.none

        | LoadPublishedDeviation ->
            let load () = loadPublishedDeviations client
            let failed ex = LoadedPublishedDeviation (Loadable.LoadingFailed ex)
            
            { model with PublishedDeviations = Loading }, Cmd.OfAsync.either load () LoadedPublishedDeviation failed

        | LoadedPublishedDeviation loadable ->
            { model with PublishedDeviations = loadable }, Cmd.none

        | AddInspiration inspiration -> failwith "todo"
        | InspirationRejected (error, inspiration) -> failwith "todo"
        
        | Inspiration2Prompt (inspiration, prompt) -> failwith "todo"
        | PromptRejected (error, inspiration, prompt) -> failwith "todo"
        
        | Prompt2LocalDeviation (prompt, local) -> failwith "todo"
        | LocalDeviationRejected (error, prompt, local) -> failwith "todo"
        
        | StashDeviation metadata -> failwith "todo"
        | StashedDeviation (local, stashed) -> failwith "todo"
        | StashFailed (error, local) -> failwith "todo"
        
        | PublishStashed stashedDeviation -> failwith "todo"
        | PublishedDeviation (stashed, published) -> failwith "todo"
        | PublishFailed (error, stashed) -> failwith "todo"
        
        | UploadLocalDeviations browserFiles -> failwith "todo"
