namespace FunSharp.ArtStudio.Client

open System
open System.Web
open FunSharp.ArtStudio.Client.Model.AddInspiration
open FunSharp.OpenAI.Api.Model.Sora
open Microsoft.Extensions.Logging
open Elmish
open FunSharp.Common
open FunSharp.Blazor.Components
open FunSharp.ArtStudio.Model
open FunSharp.ArtStudio.Client.Model

// for some reason the following causes non-sequential calls:

// task
// |> Async.AwaitTask
// |> Async.tee (fun response -> response.EnsureSuccessStatusCode() |> ignore)
// |> Async.catch
// |> AsyncResult.getOrFail

module Update =
    let private apiRoot = "http://localhost:5123/api/v1"
    
    let private loadStatefulItems<'T> client endpoint =
        
        $"{apiRoot}{endpoint}"
        |> Http.get client
        |> Async.bind Http.contentAsString
        |> Async.map (JsonSerializer.deserialize<'T array> >> Array.map StatefulItem.Default >> Loadable.Loaded)
        
    let private loadStatefulItemsPage<'T> client endpoint offset limit =
        
        let asLoadedStatefulItemsPage page =
            {
                items = page.items |> Array.map StatefulItem.Default
                offset = page.offset
                total = page.total
                has_more = page.has_more
            }
            |> Loadable.Loaded
        
        $"{apiRoot}{endpoint}?offset={offset}&limit={limit}"
        |> Http.get client
        |> Async.bind Http.contentAsString
        |> Async.map (JsonSerializer.deserialize<Page<'T>> >> asLoadedStatefulItemsPage)
        
    let private loadSettings client =
        
        $"{apiRoot}/settings"
        |> Http.get client
        |> Async.bind Http.contentAsString
        |> Async.map (JsonSerializer.deserialize<Settings> >> Loadable.Loaded)
    
    let private loadInspirations client =
        
        loadStatefulItems<Inspiration> client "/local/inspirations"
    
    let private loadPrompts client =
        
        loadStatefulItems<Prompt> client "/local/prompts"
    
    let private loadSoraTasks client =
        
        loadStatefulItems<SoraTask> client "/local/sora-tasks"
    
    let private loadSoraResults client =
        
        loadStatefulItems<SoraResult> client "/local/sora-results"
        
    let private loadLocalDeviations client offset limit =
        
        loadStatefulItemsPage<LocalDeviation> client "/local/deviations" offset limit
        
    let private loadStashedDeviations client =
        
        loadStatefulItems<StashedDeviation> client "/stash"
        
    let private loadPublishedDeviations client =
        
        loadStatefulItems<PublishedDeviation> client "/publish"
        
    let private addInspiration client inspirationUrl =
        
        inspirationUrl.ToString()
        |> HttpUtility.HtmlEncode
        |> Http.putString client $"{apiRoot}/local/inspiration"
        
    let private addPrompt client promptText =
        
        promptText
        |> HttpUtility.HtmlEncode
        |> Http.putString client $"{apiRoot}/local/prompt"
        |> Async.bind Http.contentAsString
        |> Async.map JsonSerializer.deserialize<Prompt>
        
    let private forget<'T> client (value: 'T) url =
        
        Http.delete client url
        |> Async.bind (fun _ -> value |> Async.returnM)
        
    let private updatePrompt client (prompt: Prompt) =
        
        Http.patchObject client $"{apiRoot}/local/prompt" prompt
        |> Async.bind Http.contentAsString
        |> Async.map JsonSerializer.deserialize<Prompt>
        
    let private updateLocalDeviation client (local: LocalDeviation) =
        
        Http.patchObject client $"{apiRoot}/local/deviation" local
        |> Async.bind Http.contentAsString
        |> Async.map JsonSerializer.deserialize<LocalDeviation>
        
    let private inspiration2Prompt client (inspiration: Inspiration, promptText: string) =
        
        { InspirationId = inspiration.Url; Text = promptText }
        |> Http.postObject client $"{apiRoot}/inspiration2prompt"
        |> Async.bind Http.contentAsString
        |> Async.map JsonSerializer.deserialize<Prompt>
        |> Async.map (fun prompt -> inspiration, prompt)
        
    let private prompt2LocalDeviation client (prompt: Prompt, image: Image) =
        
        Http.putFile client $"{apiRoot}/local/images" image.Name image.ContentType image.Content
        |> Async.bind Http.contentAsString
        |> Async.map (JsonSerializer.deserialize<Uri array> >> Array.head)
        |> Async.bind (fun imageUrl ->
            { PromptId = prompt.Id; ImageUrl = imageUrl }
            |> Http.postObject client $"{apiRoot}/prompt2deviation"
            |> Async.bind Http.contentAsString
            |> Async.map JsonSerializer.deserialize<LocalDeviation>
            |> Async.map (fun local -> prompt, local)
        )
        
    let private prompt2SoraTask client (prompt: Prompt, aspectRatio: AspectRatio) =
        
        { PromptId = prompt.Id; AspectRatio = aspectRatio }
        |> Http.postObject client $"{apiRoot}/prompt2sora"
        |> Async.bind Http.contentAsString
        |> Async.map JsonSerializer.deserialize<SoraTask>
        
    let private retrySora client (result: SoraResult) =
        
        { SoraResultId = result.Id }
        |> Http.postObject client $"{apiRoot}/retry-sora"
        |> Async.bind Http.contentAsString
        |> Async.map JsonSerializer.deserialize<SoraTask>
        |> Async.map (fun task -> result, task)
        
    let private soraResult2LocalDeviation client (result: SoraResult, pickedIndex: int) =
        
        { SoraResultId = result.Id; PickedIndex = pickedIndex }
        |> Http.postObject client $"{apiRoot}/sora2deviation"
        |> Async.bind Http.contentAsString
        |> Async.map JsonSerializer.deserialize<LocalDeviation>
        |> Async.map (fun local -> result, local)
        
    let private stashDeviation client (local: LocalDeviation) =
        
        local
        |> updateLocalDeviation client
        |> Async.bind (fun _ ->
            let encodedKey = local.ImageUrl.ToString() |> HttpUtility.UrlEncode
            
            Http.post client $"{apiRoot}/stash?key={encodedKey}" None
            |> Async.bind Http.contentAsString
            |> Async.map JsonSerializer.deserialize<StashedDeviation>
            |> Async.map (fun stashed -> local, stashed)
        )
        
    let private publishDeviation client (stashed: StashedDeviation) =
        
        let encodedKey = stashed.ImageUrl.ToString() |> HttpUtility.UrlEncode
        
        Http.post client $"{apiRoot}/publish?key={encodedKey}" None
        |> Async.bind Http.contentAsString
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
                Cmd.ofMsg LoadSoraTasks
                Cmd.ofMsg LoadSoraResults
                Cmd.ofMsg LoadLocalDeviations
                Cmd.ofMsg LoadStashedDeviations
                Cmd.ofMsg LoadPublishedDeviations
            ]
            
            model, batch
            
        | LoadSettings ->
            
            let load () = loadSettings client
            let failed ex = LoadedSettings (Loadable.LoadingFailed ex)
            
            { model with Settings = Loadable.Loading }, Cmd.OfAsync.either load () LoadedSettings failed
            
        | LoadedSettings settings ->
           
           { model with Settings = settings }, Cmd.none
           
        | LoadInspirations ->
            
            let load () = loadInspirations client
            let failed ex = LoadedInspirations (Loadable.LoadingFailed ex)
            
            { model with Inspirations = Loadable.Loading }, Cmd.OfAsync.either load () LoadedInspirations failed
            
        | LoadedInspirations inspirations ->
            
            { model with Inspirations = inspirations }, Cmd.none
            
        | LoadPrompts ->
            
            let load () = loadPrompts client
            let failed ex = LoadedPrompts (Loadable.LoadingFailed ex)
            
            { model with Prompts = Loadable.Loading }, Cmd.OfAsync.either load () LoadedPrompts failed
            
        | LoadedPrompts prompts ->
            
            { model with Prompts = prompts }, Cmd.none
            
        | LoadSoraTasks ->
            
            let load () = loadSoraTasks client
            let failed ex = LoadedSoraTasks (Loadable.LoadingFailed ex)
            
            { model with SoraTasks = Loadable.Loading }, Cmd.OfAsync.either load () LoadedSoraTasks failed
        
        | LoadedSoraTasks tasks ->
            
            { model with SoraTasks = tasks }, Cmd.none
        
        | LoadSoraResults ->
            
            let load () = loadSoraResults client
            let failed ex = LoadedSoraResults (Loadable.LoadingFailed ex)
            
            { model with SoraResults = Loadable.Loading }, Cmd.OfAsync.either load () LoadedSoraResults failed
        
        | LoadedSoraResults results ->
            
            { model with SoraResults = results }, Cmd.none
        
        | LoadLocalDeviations ->
            
            model, Cmd.ofMsg <| LoadLocalDeviationsPage (0, Helpers.localDeviationsPageSize)
        
        | LoadLocalDeviationsPage(offset, limit) ->
            
            let load () = loadLocalDeviations client offset limit
            let failed ex = LoadedLocalDeviationsPage (Loadable.LoadingFailed ex)
            
            { model with LocalDeviations = Loadable.Loading }, Cmd.OfAsync.either load () LoadedLocalDeviationsPage failed
            
        | LoadedLocalDeviationsPage loadable ->
            
            { model with LocalDeviations = loadable }, Cmd.none
            
        | LoadStashedDeviations ->
            
            let load () = loadStashedDeviations client
            let failed ex = LoadedStashedDeviations (Loadable.LoadingFailed ex)
            
            { model with StashedDeviations = Loadable.Loading }, Cmd.OfAsync.either load () LoadedStashedDeviations failed
            
        | LoadedStashedDeviations loadable ->
            
            { model with StashedDeviations = loadable }, Cmd.none
            
        | LoadPublishedDeviations ->
            
            let load () = loadPublishedDeviations client
            let failed ex = LoadedPublishedDeviations (Loadable.LoadingFailed ex)
            
            { model with PublishedDeviations = Loadable.Loading }, Cmd.OfAsync.either load () LoadedPublishedDeviations failed
            
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
            let succeeded _ = AddInspirationDone
            let failed ex = AddInspirationFailed ex
            
            let cmd = 
                match model.AddInspirationState.Url with
                | None ->
                    InvalidOperationException("no url set") |> failed |> Cmd.ofMsg
                | Some url ->
                    Cmd.OfAsync.either add url succeeded failed
                    
            { model with AddInspirationState.IsBusy = true; AddInspirationState.Url = None }, cmd
            
        | AddInspirationDone ->
            
            let subState = { model.AddInspirationState with IsBusy = false; Url = None; Error = None }
            
            { model with AddInspirationState = subState }, Cmd.none
            
        | AddInspirationFailed error ->
            
            printfn $"adding new inspiration failed: {error}"
            
            { model with State.AddInspirationState.Error = Some error; State.AddInspirationState.IsBusy = false }, Cmd.none
            
        | RemoveInspiration inspiration ->
            
            let inspirations = model.Inspirations |> LoadableStatefulItems.without (Inspiration.identifier inspiration)
                
            { model with Inspirations = inspirations }, Cmd.none
            
        | ForgetInspiration inspiration ->
            
            let inspirations = model.Inspirations |> LoadableStatefulItems.setBusy (Inspiration.identifier inspiration)
            
            let action () =
                $"{apiRoot}/local/inspiration?key={inspiration.Url.ToString()}"
                |> forget client inspiration
            
            { model with Inspirations = inspirations }, Cmd.OfAsync.perform action () RemoveInspiration
            
        | Inspiration2Prompt (inspiration, promptText) ->
            
            let inspirations = model.Inspirations |> LoadableStatefulItems.setBusy (Inspiration.identifier inspiration)
            
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
            
            let inspirations = model.Inspirations |> LoadableStatefulItems.setDefault (Inspiration.identifier inspiration)
            
            printfn $"inspiration2prompt failed for: {Inspiration.keyOf inspiration} -> {promptText}"
            printfn $"error: {error}"
            
            { model with Inspirations = inspirations }, Cmd.none
            
        | Inspiration2SoraTask (inspiration, promptText, aspectRatio) ->
            
            let inspirations = model.Inspirations |> LoadableStatefulItems.setBusy (Inspiration.identifier inspiration)
            
            let action () = async {
                let! _, prompt = inspiration2Prompt client (inspiration, promptText)
                let! task = prompt2SoraTask client (prompt, aspectRatio)
                
                return inspiration, task
            }
            
            let failed ex = Inspiration2SoraTaskFailed (inspiration, promptText, ex)
            
            let cmd = Cmd.OfAsync.either action () Inspiration2SoraTaskDone failed
            
            { model with Inspirations = inspirations }, cmd
        
        | Inspiration2SoraTaskDone (inspiration, task) ->
            
            let batch = Cmd.batch [
                RemoveInspiration inspiration |> Cmd.ofMsg
                AddedSoraTask task |> Cmd.ofMsg
            ]
            
            model, batch
        
        | Inspiration2SoraTaskFailed (inspiration, promptText, error) ->
            
            let inspirations = model.Inspirations |> LoadableStatefulItems.setDefault (Inspiration.identifier inspiration)
            
            printfn $"inspiration2SoraTask failed for: {Inspiration.keyOf inspiration} -> {promptText}"
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
            
        | NewPrompt2SoraTask (promptText, aspectRatio) ->
            
            let action () = async {
                let! prompt = addPrompt client promptText
                let! task = prompt2SoraTask client (prompt, aspectRatio)
                
                return task
            }
            
            let failed ex = NewPrompt2SoraTaskFailed (promptText, ex)
            
            let cmd = Cmd.OfAsync.either action () NewPrompt2SoraTaskDone failed
            
            model, cmd
        
        | NewPrompt2SoraTaskDone task ->
            
            model, Cmd.ofMsg (AddedSoraTask task)
        
        | NewPrompt2SoraTaskFailed (promptText, error) ->
            
            printfn $"newPrompt2SoraTask failed for: {promptText}"
            printfn $"error: {error}"
            
            model, Cmd.none
            
        | UpdatePrompt prompt ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.setBusy (fun x ->  x.Id = prompt.Id)
            
            let update = updatePrompt client
            let error ex = UpdatePromptFailed (prompt, ex)
            
            { model with Prompts = prompts }, Cmd.OfAsync.either update prompt UpdatedPrompt error
            
        | UpdatedPrompt prompt ->
            
            let prompts =
                model.Prompts
                |> LoadableStatefulItems.update (Prompt.identifier prompt) prompt
                |> LoadableStatefulItems.setDefault (Prompt.identifier prompt)
                
            { model with Prompts = prompts }, Cmd.none
            
        | UpdatePromptFailed (prompt, error) ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.setDefault (Prompt.identifier prompt)
            
            printfn $"UpdatePrompt failed for: {Prompt.keyOf prompt}"
            printfn $"error: {error}"
            
            { model with Prompts = prompts }, Cmd.none
            
        | RemovePrompt prompt ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.without (Prompt.identifier prompt)
                
            { model with Prompts = prompts }, Cmd.none
            
        | ForgetPrompt prompt ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.setBusy (Prompt.identifier prompt)
            
            let action () =
                $"{apiRoot}/local/prompt?key={prompt.Id.ToString()}"
                |> forget client prompt
            
            { model with Prompts = prompts }, Cmd.OfAsync.perform action () RemovePrompt
            
        | Prompt2LocalDeviation (prompt, image) ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.setBusy (fun x ->  x.Id = prompt.Id)
        
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
            
            let prompts = model.Prompts |> LoadableStatefulItems.setDefault (Prompt.identifier prompt)
            
            printfn $"prompt2local failed for: {Prompt.keyOf prompt} -> {imageFile.Name}"
            printfn $"error: {error}"
            
            { model with Prompts = prompts }, Cmd.none
            
        | Prompt2SoraTask (prompt, aspectRatio) ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.setBusy (Prompt.identifier prompt)
        
            let prompt2SoraTask = prompt2SoraTask client
            let failed ex = Prompt2SoraTaskFailed (prompt, ex)
            
            let cmd = Cmd.OfAsync.either prompt2SoraTask (prompt, aspectRatio) Prompt2SoraTaskDone failed
            
            { model with Prompts = prompts }, cmd
        
        | Prompt2SoraTaskDone task->
            
            let batch = Cmd.batch [
                RemovePrompt task.Prompt |> Cmd.ofMsg
                AddedSoraTask task |> Cmd.ofMsg
            ]
            
            model, batch
        
        | Prompt2SoraTaskFailed (prompt, error) ->
            
            let prompts = model.Prompts |> LoadableStatefulItems.setDefault (Prompt.identifier prompt)
            
            printfn $"prompt2SoraTask failed for: {Prompt.keyOf prompt}"
            printfn $"error: {error}"
            
            { model with Prompts = prompts }, Cmd.none
        
        | AddedSoraTask task ->
            
            let tasks = model.SoraTasks |> LoadableStatefulItems.withNew task
            
            { model with SoraTasks = tasks }, Cmd.none
            
        | RetrySoraResult result ->
            
            let results = model.SoraResults |> LoadableStatefulItems.setBusy (SoraResult.identifier result)
        
            let retrySora = retrySora client
            let failed ex = RetrySoraResultFailed (result, ex)
            
            let cmd = Cmd.OfAsync.either retrySora result RetriedSoraResult failed
            
            { model with SoraResults = results }, cmd
            
        | RetriedSoraResult (result, task) ->
            
            let batch = Cmd.batch [
                RemoveSoraResult result |> Cmd.ofMsg
                AddedSoraTask task |> Cmd.ofMsg
            ]
            
            model, batch
        
        | RetrySoraResultFailed (result, error) ->
            
            let results = model.SoraResults |> LoadableStatefulItems.setDefault (SoraResult.identifier result)
            
            printfn $"retrySora failed for: {SoraResult.keyOf result}"
            printfn $"error: {error}"
            
            { model with SoraResults = results }, Cmd.none
            
        | RemoveSoraResult result ->
            
            let soraResults = model.SoraResults |> LoadableStatefulItems.without (SoraResult.identifier result)
                
            { model with SoraResults = soraResults }, Cmd.none
            
        | ForgetSoraResult result ->
            
            let action () =
                $"{apiRoot}/local/sora-result?key={SoraResult.keyOf result |> fun x -> x.ToString() |> HttpUtility.UrlEncode}"
                |> forget client result
            
            model, Cmd.OfAsync.perform action () RemoveSoraResult
        
        | SoraResult2LocalDeviation (result, pickedIndex) ->
            
            let soraResults = model.SoraResults |> LoadableStatefulItems.setBusy (SoraResult.identifier result)
        
            let soraResult2LocalDeviation = soraResult2LocalDeviation client
            let failed ex = SoraResult2LocalDeviationFailed (result, pickedIndex, ex)
            
            let cmd = Cmd.OfAsync.either soraResult2LocalDeviation (result, pickedIndex) SoraResult2LocalDeviationDone failed
            
            { model with SoraResults = soraResults }, cmd
            
        | SoraResult2LocalDeviationDone (result, local) ->
            
            let batch = Cmd.batch [
                RemoveSoraResult result |> Cmd.ofMsg
                AddedLocalDeviation local |> Cmd.ofMsg
            ]
            
            model, batch
            
        | SoraResult2LocalDeviationFailed (result, pickedIndex, error) ->
            
            let soraResults = model.SoraResults |> LoadableStatefulItems.setDefault (SoraResult.identifier result)
            
            printfn $"sora2local failed for: {SoraResult.keyOf result} -> {pickedIndex}"
            printfn $"error: {error}"
            
            { model with SoraResults = soraResults }, Cmd.none
        
        | AddedLocalDeviation _ ->
            
            let currentOffset = LoadableStatefulItemsPage.offset model.LocalDeviations
            
            model, LoadLocalDeviationsPage (currentOffset, Helpers.localDeviationsPageSize) |> Cmd.ofMsg
            
        | UpdateLocalDeviation local ->
            
            let deviations =
                model.LocalDeviations
                |> LoadableStatefulItemsPage.update (LocalDeviation.identifier local) local
                
            { model with LocalDeviations = deviations }, Cmd.none
            
        | RemoveLocalDeviation local ->
            
            let deviations = model.LocalDeviations |> LoadableStatefulItemsPage.without (LocalDeviation.identifier local)
            
            { model with LocalDeviations = deviations }, Cmd.none
            
        | ForgetLocalDeviation local ->
            
            let action () =
                $"{apiRoot}/local/deviation?key={local.ImageUrl.ToString() |> HttpUtility.UrlEncode}"
                |> forget client local
            
            model, Cmd.OfAsync.perform action () RemoveLocalDeviation
            
        | StashDeviation local ->
            
            let deviations = model.LocalDeviations |> LoadableStatefulItemsPage.setBusy (LocalDeviation.identifier local)
            
            let failed ex = StashDeviationFailed (local, ex)
            
            let updateAndStash local = async {
                let! local = updateLocalDeviation client local
                return! stashDeviation client local
            }
            
            { model with LocalDeviations = deviations }, Cmd.OfAsync.either updateAndStash local StashedDeviation failed
            
        | StashedDeviation (local, stashed) ->
            
            let batch = Cmd.batch [
                RemoveLocalDeviation local |> Cmd.ofMsg
                AddedStashedDeviation stashed |> Cmd.ofMsg
            ]
            
            model, batch
            
        | StashDeviationFailed (local, error) ->
            
            let deviations =
                model.LocalDeviations
                |> LoadableStatefulItemsPage.setDefault (LocalDeviation.identifier local)
            
            printfn $"stashing failed for: {LocalDeviation.keyOf local}"
            printfn $"error: {error}"
            
            { model with LocalDeviations = deviations }, Cmd.none
            
        | AddedStashedDeviation stashed ->
            
            let deviations = model.StashedDeviations |> LoadableStatefulItems.withNew stashed
            
            { model with StashedDeviations = deviations }, Cmd.none
            
        | RemoveStashedDeviation stashed ->
            
            let deviations = model.StashedDeviations |> LoadableStatefulItems.without (StashedDeviation.identifier stashed)
            
            { model with StashedDeviations = deviations }, Cmd.none
            
        | ForgetStashedDeviation stashed ->
            
            let action () =
                $"{apiRoot}/stash?key={stashed.ImageUrl.ToString() |> HttpUtility.UrlEncode}"
                |> forget client stashed
            
            model, Cmd.OfAsync.perform action () RemoveStashedDeviation
            
        | PublishStashed stashed ->
            
            let deviations = model.StashedDeviations |> LoadableStatefulItems.setBusy (StashedDeviation.identifier stashed)
            
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
            
            let deviations = model.StashedDeviations |> LoadableStatefulItems.setDefault (StashedDeviation.identifier stashed)
            
            printfn $"publishing failed for: {StashedDeviation.keyOf stashed}"
            printfn $"error: {error}"
            
            { model with StashedDeviations = deviations }, Cmd.none
            
        | AddedPublishedDeviation published ->
            
            let deviations = model.PublishedDeviations |> LoadableStatefulItems.withNew published
            
            { model with PublishedDeviations = deviations }, Cmd.none
