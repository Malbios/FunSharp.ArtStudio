namespace FunSharp.Blazor.Components

open Bolero.Html
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Pager =
    
    let render<'T> (total: int) (limit: int) (offset: int) (onPageChanged: int -> unit) =
        
        let currentPage = (offset / limit)
        let lastPage = (total / limit)
        
        let renderButton text disabled action =
            
            Button.render <| {
                ButtonProps.defaults with
                    ButtonStyle = ButtonStyle.Base
                    Variant = Variant.Outlined
                    Text = text
                    Action = ClickAction.Sync action
                    Disabled = disabled
            }
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            "Wrap" => FlexWrap.Wrap
            "Gap" => "0.1rem"
            
            renderButton "|<" (currentPage = 0) <| fun () -> onPageChanged 0
            
            div {
                attr.style "margin-right: 1rem;"
                renderButton "<" (currentPage = 0) <| fun () -> onPageChanged <| currentPage - 1
            }
            
            for i in [0..total / limit] do
                renderButton $"{i + 1}" (currentPage = i) <| fun () -> onPageChanged i
            
            div {
                attr.style "margin-left: 1rem;"
                renderButton ">" (currentPage = lastPage) <| fun () -> onPageChanged <| currentPage + 1
            }
            
            renderButton ">|" (currentPage = lastPage) <| fun () -> onPageChanged lastPage
        }
