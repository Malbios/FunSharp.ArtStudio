namespace FunSharp.DeviantArt.Api

open System

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
    
    let private tokenRequest rootUrl (content: (string * string) list) =
        
        content
        |> fun content -> ($"{rootUrl}{tokenEndpoint}", content)
        |> Http.RequestPayload.PostWithProperties
        |> Http.request

    let authenticate config =
        
        let redirectUri = Uri.EscapeDataString config.RedirectUri
        let scope = Uri.EscapeDataString config.Scope
        
        let authUrl = $"{config.RootUrl}{authorizeEndpoint}?response_type=code&client_id={config.ClientId}&redirect_uri={redirectUri}&scope={scope}"

        printfn $"Open this URL in your browser:\n\n{authUrl}\n"
        
        [
            "grant_type", "authorization_code"
            "client_id", config.ClientId
            "client_secret", config.ClientSecret
            "code", Http.getAuthorizationCode config.Callback
            "redirect_uri", config.RedirectUri
        ]
        |> tokenRequest config.RootUrl

    let refresh config refreshToken =
    
        [
            "grant_type", "refresh_token"
            "client_id", config.ClientId
            "client_secret", config.ClientId
            "refresh_token", refreshToken
        ]
        |> tokenRequest config.RootUrl
