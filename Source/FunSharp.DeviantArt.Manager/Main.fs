module FunSharp.DeviantArt.Manager.Main

open FunSharp.DeviantArt.Manager.Model.Application
open Microsoft.AspNetCore.Components
open Microsoft.Extensions.Logging
open Elmish
open Bolero
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.View

type ClientApplication() =
    inherit ProgramComponent<State, Message>()

    override _.CssScope = CssScopes.``FunSharp.DeviantArt.Manager``
    
    [<Inject>]
    member val Logger : ILogger<ClientApplication> = Unchecked.defaultof<_> with get, set
    
    [<Inject>]
    member val IndexedDatabase = Unchecked.defaultof<IndexedDb> with get, set
    
    override this.OnInitialized() =
        
        base.OnInitialized()
        
        this.Logger.LogInformation "App was initialized!"

    override this.Program =
        
        let initialState _ = State.empty, Cmd.ofMsg Message.LoadDeviations
        
        let update = Update.update this.Logger this.IndexedDatabase
        
        let page (model: Application.State) = model.Page

        let router = Router.infer Message.SetPage page |> Router.withNotFound Page.NotFound
            
        this.Logger.LogInformation $"Serving client application from '{this.NavigationManager.BaseUri.TrimEnd('/')}'"
        
        Program.mkProgram initialState update view
        |> Program.withRouter router
