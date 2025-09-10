namespace FunSharp.DeviantArt.Manager

open System.IO
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Components.Forms
open Microsoft.Extensions.Logging
open Elmish
open FunSharp.Common
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Api.Model
open FunSharp.DeviantArt.Manager.Model

// for some reason the following causes non-sequential calls:

// task
// |> Async.AwaitTask
// |> Async.tee (fun response -> response.EnsureSuccessStatusCode() |> ignore)
// |> Async.catch
// |> AsyncResult.getOrFail

module Update =
    
    type private Image = {
        Name: string
        ContentType: string
        Content: byte array
    }
    
    let private apiRoot = "http://localhost:5123/api/v1"
    
    let private ensureSuccess (task: Task<HttpResponseMessage>) =
        
        task
        |> Async.AwaitTask
        |> Async.map _.EnsureSuccessStatusCode()
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
        
        content
        |> post client url
    
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
            Name = file.Name
            ContentType = file.ContentType
            Content = byteArray
        }
    }
    
    let private uploadImage client (file: IBrowserFile) =
        
        processUpload file
        |> Async.bind (fun image ->
            postFile client $"{apiRoot}/local/deviation/asImages" image.Name image.ContentType image.Content
            |> Async.bind contentAsString
            |> Async.map (JsonSerializer.deserialize<LocalDeviation array> >> Array.head)
        )
        
    let private updateLocalDeviation client (local: LocalDeviation) =
        
        patchObject client $"{apiRoot}/local/deviation" local
        |> Async.bind contentAsString
        
    let private stashDeviation client (local: LocalDeviation) =
        
        local
        |> updateLocalDeviation client
        |> Async.bind (fun _ ->
            $"{apiRoot}/stash"
            |> post client <| new StringContent(local.ImageUrl.ToString())
            |> Async.bind contentAsString
            |> Async.map JsonSerializer.deserialize<StashedDeviation>
            |> Async.map (fun stashed -> (local, stashed))
        )
        
    let private publishDeviation client (stashed: StashedDeviation) =
        
        $"{apiRoot}/publish"
        |> post client <| new StringContent(stashed.ImageUrl.ToString())
        |> Async.bind contentAsString
        |> Async.map JsonSerializer.deserialize<PublishedDeviation>
        |> Async.map (fun published -> (stashed, published))
    
    let update (_: ILogger) client message model =
    
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
            
        | AddInspiration (inspirationUrl, imageFile) ->
            failwith "todo"
            
        | AddedInspiration inspiration ->
            failwith "todo"
            
        | AddInspirationFailed (error, inspirationUrl, imageFile) ->
            
            printfn $"adding inspiration failed for: {inspirationUrl}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | AddPrompt promptText ->
            failwith "todo"
            
        | AddedPrompt prompt ->
            failwith "todo"
            
        | AddPromptFailed (error, promptText) ->
            
            printfn $"adding prompt failed for: {promptText}"
            printfn $"error: {error}"
            
            model, Cmd.none
        
        | Inspiration2Prompt (inspiration, promptText) ->
            failwith "todo"
            
        | Inspiration2PromptDone prompt ->
            failwith "todo"
            
        | Inspiration2PromptFailed (error, inspiration, promptText) ->
            
            printfn $"inspiration2prompt failed for: {inspiration.Url}"
            printfn $"error: {error}"
            
            model, Cmd.none

        | Prompt2LocalDeviation (prompt, imageFile) ->
            failwith "todo"
        
        | Prompt2LocalDeviationDone localDeviation ->
            failwith "todo"
            
        | Prompt2LocalDeviationFailed (error, prompt, imageFile) ->
            
            printfn $"prompt2local failed for: {prompt.Id} -> {imageFile.Name}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | AddLocalDeviation imageFile ->
            
            let upload file = uploadImage client file
            let error ex = AddLocalDeviationFailed (ex, imageFile)
            
            model, Cmd.OfAsync.either upload imageFile AddedLocalDeviation error
            
        | AddLocalDeviations imageFiles ->
            
            model, imageFiles |> Array.map (fun x -> Cmd.ofMsg (AddLocalDeviation x)) |> Cmd.batch
            
        | AddedLocalDeviation local ->
            
            let deviations =
                match model.LocalDeviations with
                | Loaded deviations ->
                    [|local|] |> Array.append deviations |> Loadable.Loaded
                | x -> x 
            
            { model with LocalDeviations = deviations }, Cmd.none
            
        | AddLocalDeviationFailed (error, imageFile) ->
            
            printfn $"adding local deviation failed for: {imageFile.Name}"
            printfn $"error: {error}"
            
            model, Cmd.none

        | UpdateLocalDeviation local ->
            
            let update = updateLocalDeviation client
            let success _ = LoadLocalDeviations
            let error ex = UpdateLocalDeviationFailed (ex, local)
            
            { model with LocalDeviations = Loading }, Cmd.OfAsync.either update local success error
            
        | UpdatedLocalDeviation local ->
            failwith "todo"

        | UpdateLocalDeviationFailed (error, local) ->
            
            printfn $"updating local deviation failed for: {local.ImageUrl}"
            printfn $"error: {error}"
            
            model, Cmd.none
        
        | StashDeviation local ->
            
            let model = {
                model with
                    LocalDeviations = Loading
                    StashedDeviations = Loading
            }
            
            let stash = stashDeviation client
            let failed ex = StashDeviationFailed (ex, local)
            
            model, Cmd.OfAsync.either stash local StashedDeviation failed
            
        | StashedDeviation (local, stashed) ->
            
            let batch = Cmd.batch [
                Cmd.ofMsg LoadLocalDeviations
                Cmd.ofMsg LoadStashedDeviations
            ]
            
            model, batch
            
        | StashDeviationFailed (error, local) ->
            
            printfn $"stashing failed for: {local.ImageUrl}"
            printfn $"error: {error}"
            
            model, Cmd.none
        
        | PublishStashed stashed ->
            
            let model = {
                model with
                    StashedDeviations = Loading
                    PublishedDeviations = Loading
            }
            
            let publish = publishDeviation client
            let failed ex = PublishStashedFailed (ex, stashed)
            
            model, Cmd.OfAsync.either publish stashed PublishedStashed failed
            
        | PublishedStashed (stashed, published) ->
            
            let batch = Cmd.batch [
                Cmd.ofMsg LoadStashedDeviations
                Cmd.ofMsg LoadPublishedDeviations
            ]
            
            model, batch
            
        | PublishStashedFailed (error, stashed) ->
            
            printfn $"publishing failed for: {stashed.ImageUrl}"
            printfn $"error: {error}"
            
            model, Cmd.none
