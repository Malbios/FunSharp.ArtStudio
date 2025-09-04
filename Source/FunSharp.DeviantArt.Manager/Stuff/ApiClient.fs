namespace FunSharp.DeviantArt.Manager

open System.Net
open System.Net.Http
open FunSharp.Common
open FunSharp.DeviantArt.Api
open Newtonsoft.Json

type ApiClient() =

    [<Literal>]    
    let rootUrl = "https://www.deviantart.com"
    
    let client =
        let handler = new HttpChandler.AutomaticDecompression <- DecompressionMethods.AlllientHandler()
        
        handler.AutomaticDecompression <- DecompressionMethods.All
        
        new HttpClient(handler)
    
    member _.UpdateAuth(accessToken: string) =
        
        client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", accessToken)
        // Http.updateAuthorization accessToken
        
    member _.WhoAmI() =
        
        Endpoints.whoAmI
        |> fun endpoint -> Http.RequestPayload.Get $"{rootUrl}{endpoint}"
        |> Http.request
        |> AsyncResult.map JsonConvert.DeserializeObject<ApiResponses.WhoAmI>
