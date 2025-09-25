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
        
    let private post (client: HttpClient) (url: string) (content: HttpContent option) =
        
        match content with
        | None ->
            client.PostAsync(url, null)
        | Some content ->
            client.PostAsync(url, content)
        |> ensureSuccess
        
    let private put (client: HttpClient) (url: string) content =
        
        client.PutAsync(url, content)
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
        
    let private postObject client url object =
        
        object
        |> JsonSerializer.serialize
        |> fun x -> new StringContent(x, Encoding.UTF8, "application/json")
        |> fun x -> post client url (Some x)
        
    let private putFile client url name mimeType content=
        
        let byteContent = new ByteArrayContent(content)
        
        byteContent.Headers.ContentType <- MediaTypeHeaderValue(mimeType)
        
        use content = new MultipartFormDataContent()
        
        content.Add(byteContent, "file", name)
        
        content
        |> put client url
        
    let private putString client url (value: String) =
        
        new StringContent(value)
        |> put client url
        
    let private patchObject client url value =
        
        value
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
        |> putString client $"{apiRoot}/local/inspiration"
        |> Async.bind contentAsString
        |> Async.map JsonSerializer.deserialize<Inspiration>
        
    let private addPrompt client promptText =
        
        promptText
        |> HttpUtility.HtmlEncode
        |> putString client $"{apiRoot}/local/prompt"
        |> Async.bind contentAsString
        |> Async.map JsonSerializer.deserialize<Prompt>
        
    let private forgetPrompt client prompt =
        
        delete client $"{apiRoot}/local/prompt?id={prompt.Id.ToString()}"
        |> Async.bind (fun _ -> prompt |> Async.returnM)
        
    let private forgetInspiration client (inspiration: Inspiration) =
        
        delete client $"{apiRoot}/local/inspiration?url={inspiration.Url.ToString()}"
        |> Async.bind (fun _ -> inspiration |> Async.returnM)
        
    let private forgetLocalDeviation client (local: LocalDeviation) =
        
        delete client $"{apiRoot}/local/deviation?url={local.ImageUrl.ToString() |> HttpUtility.UrlEncode}"
        |> Async.bind (fun _ -> local |> Async.returnM)
    
    let private uploadImage client (image: Image) =
        
        putFile client $"{apiRoot}/local/deviation/asImages" image.Name image.ContentType image.Content
        |> Async.bind contentAsString
        |> Async.map (JsonSerializer.deserialize<LocalDeviation array> >> Array.head)
        
    let private updatePrompt client (prompt: Prompt) =
        
        patchObject client $"{apiRoot}/local/prompt" prompt
        |> Async.bind contentAsString
        |> Async.map JsonSerializer.deserialize<Prompt>
        
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
        
        putFile client $"{apiRoot}/local/images" image.Name image.ContentType image.Content
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
            let encodedKey = local.ImageUrl.ToString() |> HttpUtility.UrlEncode
            
            post client $"{apiRoot}/stash?key={encodedKey}" None
            |> Async.bind contentAsString
            |> Async.map JsonSerializer.deserialize<StashedDeviation>
            |> Async.map (fun stashed -> local, stashed)
        )
        
    let private publishDeviation client (stashed: StashedDeviation) =
        
        let encodedKey = stashed.ImageUrl.ToString() |> HttpUtility.UrlEncode
        
        post client $"{apiRoot}/publish?key={encodedKey}" None
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
                { model with AddInspirationState.Error = Some error }, Cmd.none

        | AddInspiration ->
            
            let add = addInspiration client
            let failed ex = AddInspirationFailed ex
            
            let cmd = 
                match model.AddInspirationState.Url with
                | None ->
                    InvalidOperationException("no url set") |> failed |> Cmd.ofMsg
                | Some url ->
                    Cmd.OfAsync.either add url AddedInspiration failed
                    
            { model with AddInspirationState.IsBusy = true; AddInspirationState.Url = None }, cmd
            
        | AddedInspiration inspiration ->
            
            let subState = { model.AddInspirationState with IsBusy = false; Url = None; Error = None }
            let inspirations = model.Inspirations |> LoadableStatefulItems.withNew inspiration
            
            { model with Inspirations = inspirations; AddInspirationState = subState }, Cmd.none
            
        | AddInspirationFailed error ->
            
            printfn $"adding new inspiration failed: {error}"
            
            { model with State.AddInspirationState.Error = Some error; State.AddInspirationState.IsBusy = false }, Cmd.none
            
        | RemoveInspiration inspiration ->
            
            let inspirations = model.Inspirations |> LoadableStatefulItems.without (Inspiration.identifier inspiration)
                
            { model with Inspirations = inspirations }, Cmd.none
            
        | ForgetInspiration inspiration ->
            
            let inspirations = model.Inspirations |> LoadableStatefulItems.isBusy (Inspiration.identifier inspiration)
            
            let forget = forgetInspiration client
            
            { model with Inspirations = inspirations }, Cmd.OfAsync.perform forget inspiration RemoveInspiration
            
        | Inspiration2Prompt (inspiration, promptText) ->
            
            let inspirations = model.Inspirations |> LoadableStatefulItems.isBusy (Inspiration.identifier inspiration)
            
            let promptText =
                promptText.Split("\n")
                |> Array.map _.Trim()
                |> Array.filter (fun line -> String.IsNullOrWhiteSpace(line) |> not)
                |> String.concat "\n\n"
            
            let inspiration2Prompt = inspiration2Prompt client
            let failed ex = Inspiration2PromptFailed (inspiration, promptText, ex)
            
            let cmd = Cmd.OfAsync.either inspiration2Prompt (inspiration, promptText) Inspiration2PromptDone failed
            
            { model with Inspirations = inspirations }, cmd
            
        | Inspiration2PromptDone (inspiration, prompt) ->
            
            let batch = Cmd.batch [
                RemoveInspiration inspiration |> Cmd.ofMsg
                AddedPrompt prompt |> Cmd.ofMsg
            ]
            
            model, batch
            
        | Inspiration2PromptFailed (inspiration, promptText, error) ->
            
            let inspirations = model.Inspirations |> LoadableStatefulItems.isDefault (Inspiration.identifier inspiration)
            
            printfn $"inspiration2prompt failed for: {Inspiration.keyOf inspiration} -> {promptText}"
            printfn $"error: {error}"
            
            { model with Inspirations = inspirations }, Cmd.none
            
        | AddPrompt promptText ->
            
            let add = addPrompt client
            let failed ex = AddPromptFailed (promptText, ex)
            
            model, Cmd.OfAsync.either add promptText AddedPrompt failed
            
        | AddedPrompt prompt ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.withNew prompt
            
            { model with Prompts = prompts }, Cmd.none
            
        | AddPromptFailed (promptText, error) ->
            
            printfn $"adding prompt failed for: {promptText}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | UpdatePrompt prompt ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.isBusy (fun x ->  x.Id = prompt.Id)
            
            let update = updatePrompt client
            let error ex = UpdatePromptFailed (prompt, ex)
            
            { model with Prompts = prompts }, Cmd.OfAsync.either update prompt UpdatedPrompt error
            
        | UpdatedPrompt prompt ->
            
            let prompts =
                model.Prompts
                |> LoadableStatefulItems.withUpdated (Prompt.identifier prompt) prompt
                |> LoadableStatefulItems.isDefault (Prompt.identifier prompt)
                
            { model with Prompts = prompts }, Cmd.none
            
        | UpdatePromptFailed (prompt, error) ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.isDefault (Prompt.identifier prompt)
            
            printfn $"UpdatePrompt failed for: {Prompt.keyOf prompt}"
            printfn $"error: {error}"
            
            { model with Prompts = prompts }, Cmd.none
            
        | RemovePrompt prompt ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.without (Prompt.identifier prompt)
                
            { model with Prompts = prompts }, Cmd.none
            
        | ForgetPrompt prompt ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.isBusy (Prompt.identifier prompt)
            
            let forget = forgetPrompt client
            
            { model with Prompts = prompts }, Cmd.OfAsync.perform forget prompt RemovePrompt
            
        | Prompt2LocalDeviation (prompt, image) ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.isBusy (fun x ->  x.Id = prompt.Id)
        
            let prompt2LocalDeviation = prompt2LocalDeviation client
            let failed ex = Prompt2LocalDeviationFailed (prompt, image, ex)
            
            let cmd = Cmd.OfAsync.either prompt2LocalDeviation (prompt, image) Prompt2LocalDeviationDone failed
            
            { model with Prompts = prompts }, cmd
            
        | Prompt2LocalDeviationDone (prompt, local) ->
            
            let batch = Cmd.batch [
                RemovePrompt prompt |> Cmd.ofMsg
                AddedLocalDeviation local |> Cmd.ofMsg
            ]
            
            model, batch
            
        | Prompt2LocalDeviationFailed (prompt, imageFile, error) ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.isDefault (Prompt.identifier prompt)
            
            printfn $"prompt2local failed for: {Prompt.keyOf prompt} -> {imageFile.Name}"
            printfn $"error: {error}"
            
            { model with Prompts = prompts }, Cmd.none
            
        | AddedLocalDeviation local ->
            
            let deviations = model.LocalDeviations |> LoadableStatefulItems.withNew local
            
            { model with LocalDeviations = deviations }, Cmd.none
            
        | UpdateLocalDeviation local ->
            
            let deviations = model.LocalDeviations |> LoadableStatefulItems.isBusy (LocalDeviation.identifier local)
            
            let update = updateLocalDeviation client
            let failed ex = UpdateLocalDeviationFailed (local, ex)
            
            { model with LocalDeviations = deviations }, Cmd.OfAsync.either update local UpdatedLocalDeviation failed
            
        | UpdatedLocalDeviation local ->
            
            let deviations =
                model.LocalDeviations
                |> LoadableStatefulItems.withUpdated (LocalDeviation.identifier local) local
                |> LoadableStatefulItems.isDefault (LocalDeviation.identifier local)
                
            { model with LocalDeviations = deviations }, Cmd.none
            
        | UpdateLocalDeviationFailed (local, error) ->
            
            let deviations = model.LocalDeviations |> LoadableStatefulItems.isDefault (LocalDeviation.identifier local)
            
            printfn $"updating local deviation failed for: {LocalDeviation.keyOf local}"
            printfn $"error: {error}"
            
            { model with LocalDeviations = deviations }, Cmd.none
            
        | RemoveLocalDeviation local ->
            
            let deviations = model.LocalDeviations |> LoadableStatefulItems.without (LocalDeviation.identifier local)
                
            { model with LocalDeviations = deviations }, Cmd.none
            
        | ForgetLocalDeviation local ->
            
            let action = forgetLocalDeviation client
            
            model, Cmd.OfAsync.perform action local RemoveLocalDeviation
            
        | StashDeviation local ->
            
            let deviations = model.LocalDeviations |> LoadableStatefulItems.isBusy (LocalDeviation.identifier local)
            
            let stash = stashDeviation client
            let failed ex = StashDeviationFailed (local, ex)
            
            { model with LocalDeviations = deviations }, Cmd.OfAsync.either stash local StashedDeviation failed
            
        | StashedDeviation (local, stashed) ->
            
            let batch = Cmd.batch [
                RemoveLocalDeviation local |> Cmd.ofMsg
                AddedStashedDeviation stashed |> Cmd.ofMsg
            ]
            
            model, batch
            
        | StashDeviationFailed (local, error) ->
            
            let deviations = model.LocalDeviations |> LoadableStatefulItems.isDefault (LocalDeviation.identifier local)
            
            printfn $"stashing failed for: {LocalDeviation.keyOf local}"
            printfn $"error: {error}"
            
            { model with LocalDeviations = deviations }, Cmd.none
            
        | AddedStashedDeviation stashed ->
            
            let deviations = model.StashedDeviations |> LoadableStatefulItems.withNew stashed
            
            { model with StashedDeviations = deviations }, Cmd.none
            
        | RemoveStashedDeviation stashed ->
            
            let deviations = model.StashedDeviations |> LoadableStatefulItems.without (StashedDeviation.identifier stashed)
            
            { model with StashedDeviations = deviations }, Cmd.none
            
        | PublishStashed stashed ->
            
            let deviations = model.StashedDeviations |> LoadableStatefulItems.isBusy (StashedDeviation.identifier stashed)
            
            let publish = publishDeviation client
            let failed ex = PublishStashedFailed (stashed, ex)
            
            { model with StashedDeviations = deviations }, Cmd.OfAsync.either publish stashed PublishedStashed failed
            
        | PublishedStashed (stashed, published) ->
            
            let batch = Cmd.batch [
                RemoveStashedDeviation stashed |> Cmd.ofMsg
                AddedPublishedDeviation published |> Cmd.ofMsg
            ]
            
            model, batch
            
        | PublishStashedFailed (stashed, error) ->
            
            let deviations = model.StashedDeviations |> LoadableStatefulItems.isDefault (StashedDeviation.identifier stashed)
            
            printfn $"publishing failed for: {StashedDeviation.keyOf stashed}"
            printfn $"error: {error}"
            
            { model with StashedDeviations = deviations }, Cmd.none
            
        | AddedPublishedDeviation published ->
            
            let deviations = model.PublishedDeviations |> LoadableStatefulItems.withNew published
            
            { model with PublishedDeviations = deviations }, Cmd.none
