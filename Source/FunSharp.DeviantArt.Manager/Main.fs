module FunSharp.DeviantArt.Manager.Main

open Microsoft.AspNetCore.Components
open Microsoft.Extensions.Logging
open Elmish
open Bolero
open FunSharp.DeviantArt.Manager.Model
open FunSharp.DeviantArt.Manager.View

type ClientApplication() =
    inherit ProgramComponent<Application.State, Application.Message>()

    override _.CssScope = CssScopes.MyApp
    
    [<Inject>]
    member val Logger : ILogger<ClientApplication> = Unchecked.defaultof<_> with get, set
    
    override this.OnInitialized() =
        
        base.OnInitialized()
        
        this.Logger.LogInformation "App was initialized!"

    override this.Program =
        
        let initialState _ = Application.State.initial, Cmd.none
        
        let update = Update.update this.Logger
        
        let page (model: Application.State) = model.Page

        let router = Router.infer Application.Message.SetPage page |> Router.withNotFound Page.NotFound
            
        this.Logger.LogInformation $"Serving client application from '{this.NavigationManager.BaseUri.TrimEnd('/')}'"
        
        Program.mkProgram initialState update view
        |> Program.withRouter router
