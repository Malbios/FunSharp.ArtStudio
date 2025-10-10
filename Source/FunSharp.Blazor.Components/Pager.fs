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
            
            Button.render "|<" (fun () -> onPageChanged 0) (currentPage = 0)
            
            div {
                attr.style "margin-right: 1rem;"
                Button.render "<" (fun () -> onPageChanged <| currentPage - 1) (currentPage = 0)
            }
            
            for i in [0..total / limit] do
                Button.render $"{i + 1}" (fun () -> onPageChanged i) (currentPage = i)
            
            div {
                attr.style "margin-left: 1rem;"
                Button.render ">" (fun () -> onPageChanged <| currentPage + 1) (currentPage = lastPage)
            }
            
            Button.render ">|" (fun () -> onPageChanged lastPage) (currentPage = lastPage)
        }
