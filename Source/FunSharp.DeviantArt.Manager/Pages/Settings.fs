namespace FunSharp.DeviantArt.Manager.Pages

open Bolero
open Bolero.Html
open Elmish.Sub.Internal.NewSubs
open FunSharp.DeviantArt.Manager.Components
open FunSharp.DeviantArt.Manager.Model.Application
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Radzen
open Radzen.Blazor

type Settings() =
    inherit ElmishComponent<State, Message>()
    
    override _.CssScope = CssScopes.``FunSharp.DeviantArt.Manager``
        
    override this.View model dispatch =
        
        let updateClientId (newValue: string) =
            Message.UpdateAuthData { model.AuthData with ClientId = newValue } |> dispatch
        
        let updateClientSecret (newValue: string) =
            Message.UpdateAuthData { model.AuthData with ClientSecret = newValue } |> dispatch
        
        let updateAccessToken (newValue: string) =
            Message.UpdateAuthData { model.AuthData with AccessToken = newValue } |> dispatch
        
        let updateRefreshToken (newValue: string) =
            Message.UpdateAuthData { model.AuthData with RefreshToken = newValue } |> dispatch
        
        div {
            attr.``class`` "center-wrapper"
            
            comp<RadzenStack> {
                attr.style "height: 100%"

                "JustifyContent" => JustifyContent.Center
                "AlignItems" => AlignItems.Center
                
                h1 { "Settings" }
                
                TextInput.render updateClientId "Enter client id..." model.AuthData.ClientId
                
                TextInput.render updateClientSecret "Enter client secret..." model.AuthData.ClientSecret
                
                TextInput.render updateAccessToken "Enter access token..." model.AuthData.AccessToken
                
                TextInput.render updateRefreshToken "Enter refresh token..." model.AuthData.RefreshToken
                
                comp<RadzenButton> {
                    let onClick (_: MouseEventArgs) = dispatch Message.SetupClient
                    
                    "Text" => "Setup Client"
                    "Click" => EventCallback.Factory.Create<MouseEventArgs>(this, onClick)
                }
            }
        }
