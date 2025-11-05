module FunSharp.ArtStudio.Client.Main

open System
open System.Net.Http
open Microsoft.AspNetCore.Components
open Microsoft.Extensions.Logging
open Elmish
open Bolero
open FunSharp.ArtStudio.Client.Model
open FunSharp.ArtStudio.Client.View

type ClientApplication() =
    inherit ProgramComponent<State, Message>()
    
    let startBackgroundReload dispatch =
        
        let rec loop () =
            async {
                printfn "dispatching LoadSoraTasksAndResults..."
                dispatch LoadSoraTasksAndResults
                do! Async.Sleep (TimeSpan.FromSeconds(30).TotalMilliseconds |> int)
                return! loop ()
            }
            
        Async.StartImmediate (loop ())

    override _.CssScope = CssScopes.``FunSharp.ArtStudio.Client``
    
    [<Inject>]
    member val Logger : ILogger<ClientApplication> = Unchecked.defaultof<_> with get, set
    
    [<Inject>]
    member val HttpClient = Unchecked.defaultof<HttpClient> with get, set
    
    override this.OnInitialized() =
        
        base.OnInitialized()
        
        this.Logger.LogInformation "App was initialized!"

    override this.Program =
        
        let initialState _ = State.empty, Cmd.batch [ Cmd.ofMsg LoadAll; Cmd.ofEffect startBackgroundReload ]

        let update = Update.update this.Logger this.HttpClient
        
        let page (model: Model.State) = model.Page

        let router = Router.infer SetPage page |> Router.withNotFound Page.NotFound
            
        this.Logger.LogInformation $"Serving client application from '{this.NavigationManager.BaseUri.TrimEnd('/')}'"
        
        Program.mkProgram initialState update view
        |> Program.withRouter router
