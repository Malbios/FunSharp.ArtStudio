namespace FunSharp.Blazor.Components

open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components

type Injector<'T>() =
    inherit Component()
    
    [<Inject>]
    member val Injected = Unchecked.defaultof<'T> with get, set
    
    [<Parameter>]
    member val RenderFunc: 'T -> Node =  fun _ -> Node.Empty () with get, set
    
    override this.Render() =
       
       this.RenderFunc this.Injected

[<RequireQualifiedAccess>]
module Injector =
    
    let withInjected<'T> (func: 'T -> Node) =
        comp<Injector<'T>>{
           "RenderFunc" => func
        }
