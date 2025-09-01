namespace FunSharp.DeviantArt.Manager

open Blazored.LocalStorage
open Microsoft.AspNetCore.Components.WebAssembly.Hosting
open Radzen

module Program =

    [<EntryPoint>]
    let main args =
        let builder = WebAssemblyHostBuilder.CreateDefault(args)
        
        builder.RootComponents.Add<Main.ClientApplication>("#main")
        
        builder.Services
            .AddBlazoredLocalStorage()
            .AddRadzenComponents()
            |> ignore
        
        builder.Build().RunAsync() |> ignore
        
        0
