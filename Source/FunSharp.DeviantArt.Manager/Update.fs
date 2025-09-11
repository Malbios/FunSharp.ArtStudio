namespace FunSharp.DeviantArt.Manager

open System
open System.IO
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Threading.Tasks
open System.Web
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
        
    let private delete (client: HttpClient) (url: string) =
        
        client.DeleteAsync(url)
        |> ensureSuccess
        
    let private contentAsString (response: HttpResponseMessage) =
        
        response.Content.ReadAsStringAsync()
        |> Async.AwaitTask
        |> Async.catch
        |> AsyncResult.getOrFail
        
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
    
    let private loadItems<'T> client endpoint (asLoadable: string -> Loadable<'T>) =
        
        $"{apiRoot}{endpoint}"
        |> get client
        |> Async.bind contentAsString
        |> Async.map asLoadable
        
    let private loadSettings (client: HttpClient) =
        
        (JsonSerializer.deserialize<Settings> >> Loaded)
        |> loadItems client "/settings"
    
    let private loadInspirations client =
        
        (JsonSerializer.deserialize<Inspiration array> >> Loaded)
        |> loadItems client "/local/inspirations"
    
    let private loadPrompts client =
        
        (JsonSerializer.deserialize<Prompt array> >> Loaded)
        |> loadItems client "/local/prompts"
    
    let private loadLocalDeviations client =
        
        (JsonSerializer.deserialize<LocalDeviation array> >> Loaded)
        |> loadItems client "/local/deviations"
    
    let private loadStashedDeviations client =
        
        (JsonSerializer.deserialize<StashedDeviation array> >> Loaded)
        |> loadItems client "/stash"
    
    let private loadPublishedDeviations client =
        
        (JsonSerializer.deserialize<PublishedDeviation array> >> Loaded)
        |> loadItems client "/publish"
        
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
        |> Async.map JsonSerializer.deserialize<LocalDeviation>
        
    let private inspiration2Prompt client (inspiration: Inspiration, promptText: string) =
        
        { Inspiration = inspiration.Url; Text = promptText }
        |> postObject client $"{apiRoot}/inspiration2prompt"
        |> Async.bind contentAsString
        |> Async.map JsonSerializer.deserialize<Prompt>
        |> Async.map (fun prompt -> inspiration, prompt)
        
    let private prompt2LocalDeviation client (prompt: Prompt, imageFile: IBrowserFile) =
        
        processUpload imageFile
        |> Async.bind (fun image ->
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
            
        | AddInspiration inspirationUrl ->
            
            let add = addInspiration client
            let failed ex = AddInspirationFailed (ex, inspirationUrl)
            
            model, Cmd.OfAsync.either add inspirationUrl AddedInspiration failed
            
        | AddedInspiration inspiration ->
            
            let inspirations =
                match model.Inspirations with
                | Loaded inspirations ->
                    [|inspiration|] |> Array.append inspirations |> Loaded
                | x -> x 
            
            { model with Inspirations = inspirations }, Cmd.none
            
        | AddInspirationFailed (error, inspirationUrl) ->
            
            printfn $"adding inspiration failed for: {inspirationUrl}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | RemoveInspiration inspiration ->
            
            let inspirations =
                match model.Inspirations with
                | Loaded inspirations ->
                    inspirations |> Array.filter (fun x -> x.Url <> inspiration.Url) |> Loaded
                | x -> x
            
            { model with Inspirations = inspirations }, Cmd.none
            
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
            
            let prompts =
                match model.Prompts with
                | Loaded prompts ->
                    [|prompt|] |> Array.append prompts |> Loaded
                | x -> x 
            
            { model with Prompts = prompts }, Cmd.none
            
        | AddPromptFailed (error, promptText) ->
            
            printfn $"adding prompt failed for: {promptText}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | RemovePrompt prompt ->
            
            let prompts =
                match model.Prompts with
                | Loaded prompts ->
                    prompts |> Array.filter (fun x -> x.Id <> prompt.Id) |> Loaded
                | x -> x
                
            { model with Prompts = prompts }, Cmd.none
            
        | ForgetPrompt prompt ->
            
            let action =
                forgetPrompt client
            
            model, Cmd.OfAsync.perform action prompt RemovePrompt
            
        | Prompt2LocalDeviation (prompt, imageFile) ->
            
            let prompt2LocalDeviation = prompt2LocalDeviation client
            let failed ex = Prompt2LocalDeviationFailed (ex, prompt, imageFile)
            
            model, Cmd.OfAsync.either prompt2LocalDeviation (prompt, imageFile) Prompt2LocalDeviationDone failed
            
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
            
        | AddLocalDeviation imageFile ->
            
            let upload file = uploadImage client file
            let error ex = AddLocalDeviationFailed (ex, imageFile)
            
            model, Cmd.OfAsync.either upload imageFile AddedLocalDeviation error
            
        | AddLocalDeviations imageFiles ->
            
            let batch = Cmd.batch (
                imageFiles
                |> Array.map (AddLocalDeviation >> Cmd.ofMsg)
            )
            
            model, batch
            
        | AddedLocalDeviation local ->
            
            let deviations =
                match model.LocalDeviations with
                | Loaded deviations ->
                    [|local|] |> Array.append deviations |> Loaded
                | x -> x 
            
            { model with LocalDeviations = deviations }, Cmd.none
            
        | AddLocalDeviationFailed (error, imageFile) ->
            
            printfn $"adding local deviation failed for: {imageFile.Name}"
            printfn $"error: {error}"
            
            model, Cmd.none

        | UpdateLocalDeviation local ->
            
            let update = updateLocalDeviation client
            let error ex = UpdateLocalDeviationFailed (ex, local)
            
            { model with LocalDeviations = Loading }, Cmd.OfAsync.either update local UpdatedLocalDeviation error
            
        | UpdatedLocalDeviation local ->
            
            match model.LocalDeviations with
            | Loaded deviations ->
                let index = deviations |> Array.findIndex (fun x -> x.ImageUrl = local.ImageUrl)
                deviations[index] <- local
            | _ -> ()
            
            model, Cmd.none

        | UpdateLocalDeviationFailed (error, local) ->
            
            printfn $"updating local deviation failed for: {local.ImageUrl}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | RemoveLocalDeviation local ->
            
            let deviations =
                match model.LocalDeviations with
                | Loaded deviations ->
                    deviations |> Array.filter (fun x -> x.ImageUrl <> local.ImageUrl) |> Loaded
                | x -> x
                
            { model with LocalDeviations = deviations }, Cmd.none
        
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
            
            let deviations =
                match model.StashedDeviations with
                | Loaded deviations ->
                    [|stashed|] |> Array.append deviations |> Loaded
                | x -> x 
            
            { model with StashedDeviations = deviations }, Cmd.none
            
        | RemoveStashedDeviation stashed ->
            
            let deviations =
                match model.StashedDeviations with
                | Loaded deviations ->
                    deviations |> Array.filter (fun x -> x.ImageUrl <> stashed.ImageUrl) |> Loaded
                | x -> x
                
            { model with StashedDeviations = deviations }, Cmd.none
        
        | PublishStashed stashed ->
            
            let publish = publishDeviation client
            let failed ex = PublishStashedFailed (ex, stashed)
            
            model, Cmd.OfAsync.either publish stashed PublishedStashed failed
            
        | PublishedStashed (stashed, published) ->
            
            let stashed =
                match model.StashedDeviations with
                | Loaded deviations ->
                    deviations |> Array.filter (fun x -> x.ImageUrl <> stashed.ImageUrl) |> Loaded
                | x -> x
                
            let published =
                match model.PublishedDeviations with
                | Loaded deviations ->
                    [|published|] |> Array.append deviations |> Loaded
                | x -> x
            
            { model with StashedDeviations = stashed; PublishedDeviations = published }, Cmd.none
            
        | PublishStashedFailed (error, stashed) ->
            
            printfn $"publishing failed for: {stashed.ImageUrl}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | AddedPublishedDeviation published ->
            
            let deviations =
                match model.PublishedDeviations with
                | Loaded deviations ->
                    [|published|] |> Array.append deviations |> Loaded
                | x -> x 
            
            { model with PublishedDeviations = deviations }, Cmd.none
