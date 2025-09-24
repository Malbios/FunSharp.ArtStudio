namespace FunSharp.DeviantArt.Manager

open System
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Threading.Tasks
open System.Web
open FunSharp.DeviantArt.Manager.Model.AddInspiration
open Microsoft.Extensions.Logging
open Elmish
open FunSharp.Common
open FunSharp.Blazor.Components
open FunSharp.DeviantArt.Model
open FunSharp.DeviantArt.Manager.Model

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
        |> Async.bind (fun response ->
            if response.IsSuccessStatusCode then
                response |> Async.returnM
            else
                response.Content.ReadAsStringAsync()
                |> Async.AwaitTask
                |> Async.map failwith
        )
        |> Async.getOrFail
    
    let private get (client: HttpClient) (url: string) =
        
        client.GetAsync(url)
        |> ensureSuccess
        
    let private post (client: HttpClient) (url: string) content =
        
        client.PostAsync(url, content)
        |> ensureSuccess
        
    let private patch (client: HttpClient) (url: string) content =
        
        client.PatchAsync(url, content)
        |> ensureSuccess
        
    let private delete (client: HttpClient) (url: string) =
        
        client.DeleteAsync(url)
        |> ensureSuccess
        
    let private contentAsString (response: HttpResponseMessage) =
        
        response.Content.ReadAsStringAsync()
        |> Async.AwaitTask
        |> Async.getOrFail
        
    let private postString client url value =
        
        new StringContent(value)
        |> post client url
        
    let private postObject client url object =
        
        object
        |> JsonSerializer.serialize
        |> fun x -> new StringContent(x, Encoding.UTF8, "application/json")
        |> post client url
        
    let private postFile client url name mimeType content=
        
        let byteContent = new ByteArrayContent(content)
        
        byteContent.Headers.ContentType <- MediaTypeHeaderValue(mimeType)
        
        use content = new MultipartFormDataContent()
        
        content.Add(byteContent, "file", name)
        
        content
        |> post client url
        
    let private patchObject client url object =
        
        object
        |> JsonSerializer.serialize
        |> fun x -> new StringContent(x, Encoding.UTF8, "application/json")
        |> patch client url
    
    let private loadStatefulItems<'T> client endpoint =
        
        $"{apiRoot}{endpoint}"
        |> get client
        |> Async.bind contentAsString
        |> Async.map (JsonSerializer.deserialize<'T array> >> Array.map StatefulItem.Default >> Loadable.Loaded)
        
    let private loadSettings (client: HttpClient) =
        
        $"{apiRoot}/settings"
        |> get client
        |> Async.bind contentAsString
        |> Async.map (JsonSerializer.deserialize<Settings> >> Loadable.Loaded)
    
    let private loadInspirations client =
        
        loadStatefulItems<Inspiration> client "/local/inspirations"
    
    let private loadPrompts client =
        
        loadStatefulItems<Prompt> client "/local/prompts"
        
    let private loadLocalDeviations client =
        
        loadStatefulItems<LocalDeviation> client "/local/deviations"
        
    let private loadStashedDeviations client =
        
        loadStatefulItems<StashedDeviation> client "/stash"
        
    let private loadPublishedDeviations client =
        
        loadStatefulItems<PublishedDeviation> client "/publish"
        
    let private addInspiration client inspirationUrl =
        
        inspirationUrl.ToString()
        |> HttpUtility.HtmlEncode
        |> postString client $"{apiRoot}/local/inspiration"
        |> Async.bind contentAsString
        |> Async.map JsonSerializer.deserialize<Inspiration>
        
    let private addPrompt client promptText =
        
        promptText
        |> HttpUtility.HtmlEncode
        |> postString client $"{apiRoot}/local/prompt"
        |> Async.bind contentAsString
        |> Async.map JsonSerializer.deserialize<Prompt>
        
    let private forgetPrompt client prompt =
        
        delete client $"{apiRoot}/local/prompt?id={prompt.Id.ToString()}"
        |> Async.bind (fun _ -> prompt |> Async.returnM)
        
    let private forgetInspiration client (inspiration: Inspiration) =
        
        delete client $"{apiRoot}/local/inspiration?url={inspiration.Url.ToString()}"
        |> Async.bind (fun _ -> inspiration |> Async.returnM)
        
    let private deleteLocalDeviation client (local: LocalDeviation) =
        
        delete client $"{apiRoot}/local/deviation?url={local.ImageUrl.ToString() |> HttpUtility.UrlEncode}"
        |> Async.bind (fun _ -> local |> Async.returnM)
    
    let private uploadImage client (image: Image) =
        
        postFile client $"{apiRoot}/local/deviation/asImages" image.Name image.ContentType image.Content
        |> Async.bind contentAsString
        |> Async.map (JsonSerializer.deserialize<LocalDeviation array> >> Array.head)
        
    let private updateLocalDeviation client (local: LocalDeviation) =
        
        patchObject client $"{apiRoot}/local/deviation" local
        |> Async.bind contentAsString
        |> Async.map JsonSerializer.deserialize<LocalDeviation>
        
    let private inspiration2Prompt client (inspiration: Inspiration, promptText: string) =
        
        { Inspiration = inspiration.Url; Text = promptText }
        |> postObject client $"{apiRoot}/inspiration2prompt"
        |> Async.bind contentAsString
        |> Async.map JsonSerializer.deserialize<Prompt>
        |> Async.map (fun prompt -> inspiration, prompt)
        
    let private prompt2LocalDeviation client (prompt: Prompt, image: Image) =
        
        postFile client $"{apiRoot}/local/images" image.Name image.ContentType image.Content
        |> Async.bind contentAsString
        |> Async.map (JsonSerializer.deserialize<Uri array> >> Array.head)
        |> Async.bind (fun imageUrl ->
            { Prompt = prompt.Id; ImageUrl = imageUrl }
            |> postObject client $"{apiRoot}/prompt2deviation"
            |> Async.bind contentAsString
            |> Async.map JsonSerializer.deserialize<LocalDeviation>
            |> Async.map (fun local -> prompt, local)
        )
        
    let private stashDeviation client (local: LocalDeviation) =
        
        local
        |> updateLocalDeviation client
        |> Async.bind (fun _ ->
            local.ImageUrl.ToString()
            |> postString client $"{apiRoot}/stash"
            |> Async.bind contentAsString
            |> Async.map JsonSerializer.deserialize<StashedDeviation>
            |> Async.map (fun stashed -> local, stashed)
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
            
        | LoadAll ->
            
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
            let failed ex = LoadedSettings (LoadingFailed ex)
            
            { model with Settings = Loading }, Cmd.OfAsync.either load () LoadedSettings failed
            
        | LoadedSettings loadable ->
           
           { model with Settings = loadable }, Cmd.none
           
        | LoadInspirations ->
            
            let load () = loadInspirations client
            let failed ex = LoadedInspirations (LoadingFailed ex)
            
            { model with Inspirations = Loading }, Cmd.OfAsync.either load () LoadedInspirations failed
            
        | LoadedInspirations loadable ->
            
            { model with Inspirations = loadable }, Cmd.none
            
        | LoadPrompts ->
            
            let load () = loadPrompts client
            let failed ex = LoadedPrompts (LoadingFailed ex)
            
            { model with Prompts = Loading }, Cmd.OfAsync.either load () LoadedPrompts failed
            
        | LoadedPrompts loadable ->
            
            { model with Prompts = loadable }, Cmd.none
            
        | LoadLocalDeviations ->
            
            let load () = loadLocalDeviations client
            let failed ex = LoadedLocalDeviations (LoadingFailed ex)
            
            { model with LocalDeviations = Loading }, Cmd.OfAsync.either load () LoadedLocalDeviations failed
            
        | LoadedLocalDeviations loadable ->
            
            { model with LocalDeviations = loadable }, Cmd.none
            
        | LoadStashedDeviations ->
            
            let load () = loadStashedDeviations client
            let failed ex = LoadedStashedDeviations (LoadingFailed ex)
            
            { model with StashedDeviations = Loading }, Cmd.OfAsync.either load () LoadedStashedDeviations failed
            
        | LoadedStashedDeviations loadable ->
            
            { model with StashedDeviations = loadable }, Cmd.none
            
        | LoadPublishedDeviations ->
            
            let load () = loadPublishedDeviations client
            let failed ex = LoadedPublishedDeviations (LoadingFailed ex)
            
            { model with PublishedDeviations = Loading }, Cmd.OfAsync.either load () LoadedPublishedDeviations failed
            
        | LoadedPublishedDeviations loadable ->
            
            { model with PublishedDeviations = loadable }, Cmd.none
            
        | ChangeNewInspirationUrl url ->
            
            match Uri.tryParse url with
            | Some uri ->
                { model with AddInspirationState.Url = Some uri; AddInspirationState.Error = None }, Cmd.none
            | None ->
                let error = InvalidOperationException($"could not parse url: '{url}'")
                let model = { model with AddInspirationState.Error = Some error }
                    
                model, Cmd.none

        | AddInspiration ->
            
            let add = addInspiration client
            let failed ex = AddInspirationFailed ex
            
            let cmd = 
                match model.AddInspirationState.Url with
                | None ->
                    InvalidOperationException("no url set") |> failed |> Cmd.ofMsg
                | Some url ->
                    Cmd.OfAsync.either add url AddedInspiration failed
            
            let model = { model with AddInspirationState.IsBusy = true; AddInspirationState.Url = None }
            
            model, cmd
            
        | AddedInspiration inspiration ->
            
            let subState = { model.AddInspirationState with IsBusy = false; Url = None; Error = None }
            let inspirations = model.Inspirations |> LoadableStatefulItemArray.withNew inspiration
            
            { model with Inspirations = inspirations; AddInspirationState = subState }, Cmd.none
            
        | AddInspirationFailed error ->
            
            printfn $"adding inspiration failed: {error}"
            
            let model = { model with State.AddInspirationState.Error = Some error; State.AddInspirationState.IsBusy = false }
            
            model, Cmd.none
            
        | RemoveInspiration inspiration ->
            
            let inspirations =
                model.Inspirations
                |> LoadableStatefulItemArray.without (fun x ->
                    Inspiration.keyOf x <> Inspiration.keyOf inspiration
                )
                
            { model with Inspirations = inspirations }, Cmd.none
            
        | ForgetInspiration inspiration ->
            
            let action = forgetInspiration client
            
            model, Cmd.OfAsync.perform action inspiration RemoveInspiration
            
        | Inspiration2Prompt (inspiration, promptText) ->
            
            let inspiration2Prompt = inspiration2Prompt client
            let failed ex = Inspiration2PromptFailed (ex, inspiration, promptText)
            
            model, Cmd.OfAsync.either inspiration2Prompt (inspiration, promptText) Inspiration2PromptDone failed
            
        | Inspiration2PromptDone (inspiration, prompt) ->
            
            let batch = Cmd.batch [
                RemoveInspiration inspiration |> Cmd.ofMsg
                AddedPrompt prompt |> Cmd.ofMsg
            ]
            
            model, batch
            
        | Inspiration2PromptFailed (error, inspiration, promptText) ->
            
            printfn $"inspiration2prompt failed for: {inspiration.Url} -> {promptText}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | AddPrompt promptText ->
            
            let add = addPrompt client
            let failed ex = AddPromptFailed (ex, promptText)
            
            model, Cmd.OfAsync.either add promptText AddedPrompt failed
            
        | AddedPrompt prompt ->
            
            let prompts = model.Prompts |> LoadableStatefulItemArray.withNew prompt
            
            { model with Prompts = prompts }, Cmd.none
            
        | AddPromptFailed (error, promptText) ->
            
            printfn $"adding prompt failed for: {promptText}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | RemovePrompt prompt ->
            
            let prompts =
                model.Prompts
                |> LoadableStatefulItemArray.without (fun x ->
                    Prompt.keyOf x <> Prompt.keyOf prompt
                )
                
            { model with Prompts = prompts }, Cmd.none
            
        | ForgetPrompt prompt ->
            
            let action = forgetPrompt client
                
            model, Cmd.OfAsync.perform action prompt RemovePrompt
            
        | Prompt2LocalDeviation (prompt, image) ->
        
            let prompt2LocalDeviation = prompt2LocalDeviation client
            let failed ex = Prompt2LocalDeviationFailed (ex, prompt, image)
            
            let model = { model with Prompts = model.Prompts |> State.isBusy (fun x ->  x.Id = prompt.Id) }
            
            model, Cmd.OfAsync.either prompt2LocalDeviation (prompt, image) Prompt2LocalDeviationDone failed
            
        | Prompt2LocalDeviationDone (prompt, local) ->
            
            let batch = Cmd.batch [
                RemovePrompt prompt |> Cmd.ofMsg
                AddedLocalDeviation local |> Cmd.ofMsg
            ]
            
            model, batch
            
        | Prompt2LocalDeviationFailed (error, prompt, imageFile) ->
            
            printfn $"prompt2local failed for: {prompt.Id} -> {imageFile.Name}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | AddedLocalDeviation local ->
            
            let deviations = model.LocalDeviations |> LoadableStatefulItemArray.withNew local
            
            { model with LocalDeviations = deviations }, Cmd.none
            
        | UpdateLocalDeviation local ->
            
            let update = updateLocalDeviation client
            let error ex = UpdateLocalDeviationFailed (ex, local)
            
            { model with LocalDeviations = Loading }, Cmd.OfAsync.either update local UpdatedLocalDeviation error
            
        | UpdatedLocalDeviation _ ->
            
            model, Cmd.ofMsg LoadLocalDeviations
            
        | UpdateLocalDeviationFailed (error, local) ->
            
            printfn $"updating local deviation failed for: {local.ImageUrl}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | RemoveLocalDeviation local ->
            
            let deviations =
                model.LocalDeviations
                |> LoadableStatefulItemArray.without (fun x ->
                    LocalDeviation.keyOf x <> LocalDeviation.keyOf local
                )
                
            { model with LocalDeviations = deviations }, Cmd.none
            
        | ForgetLocalDeviation local ->
            
            let action = deleteLocalDeviation client
            
            model, Cmd.OfAsync.perform action local RemoveLocalDeviation
            
        | StashDeviation local ->
            
            let stash = stashDeviation client
            let failed ex = StashDeviationFailed (ex, local)
            
            model, Cmd.OfAsync.either stash local StashedDeviation failed
            
        | StashedDeviation (local, stashed) ->
            
            let batch = Cmd.batch [
                RemoveLocalDeviation local |> Cmd.ofMsg
                AddedStashedDeviation stashed |> Cmd.ofMsg
            ]
            
            model, batch
            
        | StashDeviationFailed (error, local) ->
            
            printfn $"stashing failed for: {local.ImageUrl}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | AddedStashedDeviation stashed ->
            
            let deviations = model.StashedDeviations |> LoadableStatefulItemArray.withNew stashed
            
            { model with StashedDeviations = deviations }, Cmd.none
            
        | RemoveStashedDeviation stashed ->
            
            let deviations =
                model.StashedDeviations
                |> LoadableStatefulItemArray.without (fun x ->
                    StashedDeviation.keyOf x <> StashedDeviation.keyOf stashed
                )
                
            { model with StashedDeviations = deviations }, Cmd.none
        
        | PublishStashed stashed ->
            
            let publish = publishDeviation client
            let failed ex = PublishStashedFailed (ex, stashed)
            
            model, Cmd.OfAsync.either publish stashed PublishedStashed failed
            
        | PublishedStashed (stashed, published) ->
            
            let batch = Cmd.batch [
                RemoveStashedDeviation stashed |> Cmd.ofMsg
                AddedPublishedDeviation published |> Cmd.ofMsg
            ]
            
            model, batch
            
        | PublishStashedFailed (error, stashed) ->
            
            printfn $"publishing failed for: {stashed.ImageUrl}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | AddedPublishedDeviation published ->
            
            let deviations = model.PublishedDeviations |> LoadableStatefulItemArray.withNew published
            
            { model with PublishedDeviations = deviations }, Cmd.none
