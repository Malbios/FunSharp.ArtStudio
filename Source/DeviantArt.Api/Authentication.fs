namespace DeviantArt.Api

open System

[<RequireQualifiedAccess>]
module Authentication =
    let private authorizeEndpoint = "/oauth2/authorize"
    let private tokenEndpoint = "/oauth2/token"

    type Configuration = {
        RootUrl: string
        ClientId: string
        ClientSecret: string
        RedirectUri: string
        Scope: string
        Callback: Http.CallbackConfiguration
    }

    let private authWithCode config authorizationCode =

        [
            "grant_type", "authorization_code"
            "client_id", config.ClientId
            "client_secret", config.ClientSecret
            "code", authorizationCode
            "redirect_uri", config.RedirectUri
        ]
        |> Map.ofList
        |> Some
        |> Http.request $"{config.RootUrl}{tokenEndpoint}"

    let authenticate config =
        
        let redirectUri = Uri.EscapeDataString config.RedirectUri
        let scope = Uri.EscapeDataString config.Scope
        
        let authUrl =
            $"{config.RootUrl}{authorizeEndpoint}?response_type=code&client_id={config.ClientId}&redirect_uri={redirectUri}&scope={scope}"

        printfn $"Open this URL in your browser:\n\n{authUrl}\n"

        Http.getAuthorizationCode config.Callback |> authWithCode config

    let refresh config refreshToken =
    
        [
            "grant_type", "refresh_token"
            "client_id", config.ClientId
            "client_secret", config.ClientId
            "refresh_token", refreshToken
        ]
        |> Map.ofList
        |> Some
        |> Http.request $"{config.RootUrl}{tokenEndpoint}"
