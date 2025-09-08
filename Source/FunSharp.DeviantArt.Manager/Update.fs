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
open Microsoft.Extensions.Logging

// for some reason the following causes non-sequential calls:

// task
// |> Async.AwaitTask
// |> Async.tee (fun response -> response.EnsureSuccessStatusCode() |> ignore)
// |> Async.catch
// |> AsyncResult.getOrFail

module Update =
    
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
        
        return Image(file.Name, file.ContentType, byteArray)
    }
    
    let private uploadImage client (file: IBrowserFile) =
        
        processUpload file
        |> Async.bind (fun image ->
            postFile client $"{apiRoot}/local/deviation/asImages" image.Name image.MimeType image.Content
            |> Async.bind contentAsString
            |> Async.map (JsonSerializer.deserialize<(LocalDeviation * Image) array> >> Array.head)
        )
        
    let private updateLocalDeviation client (local: LocalDeviation) =
        
        patchObject client $"{apiRoot}/local/deviation" local
        |> Async.bind contentAsString
        
    let private stashDeviation client (local: LocalDeviation) =
        
        local
        |> updateLocalDeviation client
        |> Async.bind (fun _ ->
            $"{apiRoot}/stash"
            |> post client <| new StringContent(local.Title)
            |> Async.bind contentAsString
            |> Async.map JsonSerializer.deserialize<StashedDeviation>
            |> Async.map (fun stashed -> (local, stashed))
        )
        
    let private publishDeviation client (stashed: StashedDeviation) =
        
        $"{apiRoot}/publish"
        |> post client <| new StringContent(stashed.Metadata.Id)
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
            
        | LoadImage id ->
            failwith "todo"
        
        | LoadedImage (id, image) ->
            failwith "todo"
        
        | LoadImageFailed (error, id) ->
            
            printfn $"loading image failed for: {id}"
            printfn $"error: {error}"
            
            model, Cmd.none

        | AddInspiration inspiration ->
            failwith "todo"
            
        | AddInspirationFailed (error, inspiration) ->
            
            printfn $"adding inspiration failed for: {inspiration.Url}"
            printfn $"error: {error}"
            
            model, Cmd.none
        
        | Inspiration2Prompt (inspiration, prompt) ->
            failwith "todo"
            
        | Inspiration2PromptFailed (error, inspiration, prompt) ->
            
            printfn $"inspiration2prompt failed for: {inspiration.Url} -> {prompt.Text}"
            printfn $"error: {error}"
            
            model, Cmd.none
        
        | Prompt2LocalDeviation (prompt, local) ->
            failwith "todo"
            
        | Prompt2LocalDeviationFailed (error, prompt, local) ->
            
            printfn $"prompt2local failed for: {prompt.Text} -> {local.Title}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | ProcessImages files ->
            
            let upload = uploadImage client
            let error ex file = ProcessImageFailed (ex, file)
            
            let batch =
                files
                |> Array.map (fun file ->
                    Cmd.OfAsync.either upload file ProcessedImage (fun ex -> error ex file)
                )
                |> Cmd.batch
            
            model, batch
            
        | ProcessedImage (local, image) ->
            
            let localDeviations =
                match model.LocalDeviations with
                | Loaded deviations -> [|local|] |> Array.append deviations |> Loadable.Loaded
                | _ -> failwith $"Could not add local deviation because model.LocalDeviations is not loaded"
                
            let images = model.Images |> Map.add local.Id (Loadable.Loaded image) 
            
            let model = {
                model with
                    LocalDeviations = localDeviations
                    Images = images
            }
            
            model, Cmd.none

        | ProcessImageFailed (error, file) ->
            
            printfn $"image processing failed for: {file.Name}"
            printfn $"error: {error}"
            
            model, Cmd.none

        | UpdateLocalDeviation local ->
            
            let update = updateLocalDeviation client
            let success _ = LoadLocalDeviations
            let error ex = UpdateLocalDeviationFailed (ex, local)
            
            { model with LocalDeviations = Loading }, Cmd.OfAsync.either update local success error

        | UpdateLocalDeviationFailed (error, local) ->
            
            printfn $"updating local deviation failed for: {local.Id}"
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
            
            printfn $"stashing failed for: {local.Id}"
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
            
            printfn $"publishing failed for: {stashed.Metadata.Id}"
            printfn $"error: {error}"
            
            model, Cmd.none
