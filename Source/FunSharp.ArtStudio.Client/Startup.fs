namespace FunSharp.ArtStudio.Client

open Microsoft.AspNetCore.Components.WebAssembly.Hosting
open Microsoft.Extensions.DependencyInjection
open Radzen

module Program =

    [<EntryPoint>]
    let main args =
        let builder = WebAssemblyHostBuilder.CreateDefault(args)
        
        builder.RootComponents.Add<Main.ClientApplication>("#main")
        
        builder.Services
            .AddRadzenComponents()
            .AddHttpClient()
        |> ignore
        
        builder.Build().RunAsync() |> ignore
        
        0
