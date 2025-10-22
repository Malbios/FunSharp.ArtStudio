namespace FunSharp.Blazor.Components

open Bolero.Html
open Radzen
open Radzen.Blazor

[<RequireQualifiedAccess>]
module Pager =
    
    let render<'T> (total: int) (limit: int) (offset: int) (onPageChanged: int -> unit) =
        
        let currentPage = (offset / limit)
        let lastPage = (total / limit)
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            "Wrap" => FlexWrap.Wrap
            "Gap" => "0.1rem"
            
            Button.render <| {
                ButtonProps.defaults with
                    Text = "|<"
                    Action = ClickAction.Sync <| fun () -> onPageChanged 0
                    Disabled = currentPage = 0
            }
            
            div {
                attr.style "margin-right: 1rem;"
                
                Button.render <| {
                    ButtonProps.defaults with
                        Text = "<"
                        Action = ClickAction.Sync <| fun () -> onPageChanged <| currentPage - 1
                        Disabled = currentPage = 0
                }
            }
            
            for i in [0..total / limit] do
                Button.render <| {
                    ButtonProps.defaults with
                        Text = $"{i + 1}"
                        Action = ClickAction.Sync <| fun () -> onPageChanged i
                        Disabled = currentPage = i
                }
            
            div {
                attr.style "margin-left: 1rem;"
                
                Button.render <| {
                    ButtonProps.defaults with
                        Text = ">"
                        Action = ClickAction.Sync <| fun () -> onPageChanged <| currentPage + 1
                        Disabled = currentPage = lastPage
                }
            }
                
            Button.render <| {
                ButtonProps.defaults with
                    Text = ">|"
                    Action = ClickAction.Sync <| fun () -> onPageChanged lastPage
                    Disabled = currentPage = lastPage
            }
        }
