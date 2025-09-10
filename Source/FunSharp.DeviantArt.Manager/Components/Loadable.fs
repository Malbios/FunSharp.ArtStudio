namespace FunSharp.DeviantArt.Manager.Components

open Bolero
open Bolero.Html
open FunSharp.DeviantArt.Manager.Model

[<RequireQualifiedAccess>]
module Loadable =
    
    let render<'T> (item: Loadable<'T>) (renderAction: 'T -> Node) =
        
        match item with
        | Loaded data ->
            renderAction data
        
        | LoadingFailed error ->
            concat {
                p { error.Message }
                p { error.StackTrace }
            }
            
        | _ ->
            LoadingWidget.render ()
