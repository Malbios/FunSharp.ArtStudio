namespace FunSharp.Blazor.Components

open Bolero
open Bolero.Html

[<RequireQualifiedAccess>]
type Loadable<'T> =
    | NotLoaded
    | Loading
    | Loaded of 'T
    | LoadingFailed of exn

[<RequireQualifiedAccess>]
module Loadable =
    
    let render<'T> (item: Loadable<'T>) (renderAction: 'T -> Node) =
        
        match item with
        | Loadable.Loaded data ->
            renderAction data
        
        | Loadable.LoadingFailed error ->
            concat {
                p { error.Message }
                p { error.StackTrace }
            }
            
        | _ ->
            LoadingWidget.render ()
