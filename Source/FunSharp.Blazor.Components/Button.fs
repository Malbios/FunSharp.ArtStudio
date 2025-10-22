namespace FunSharp.Blazor.Components

open System.Threading.Tasks
open Bolero.Html
open Microsoft.AspNetCore.Components.Web
open Radzen
open Radzen.Blazor

type ClickAction =
    | Sync of action: (unit -> unit)
    | Async of action: (unit -> Task)

type ButtonProps ={
    Shade: Shade 
    Variant: Variant
    ButtonStyle: ButtonStyle
    Disabled: bool
    Action: ClickAction
    Text: string
    Icon: string
    IsBusy: bool
    BusyText: string
}

[<RequireQualifiedAccess>]
module ButtonProps =
    
    let defaults = {
        Shade = Shade.Default
        Variant = Variant.Filled
        ButtonStyle = ButtonStyle.Primary
        Disabled = false
        Action = ClickAction.Sync <| fun () -> ()
        Text = ""
        Icon = ""
        IsBusy = false
        BusyText = ""
    }


[<RequireQualifiedAccess>]
module Button =
    
    let render (properties: ButtonProps) =
        
        comp<RadzenButton> {
            "Variant" => properties.Variant
            "ButtonStyle" => properties.ButtonStyle
            "Disabled" => properties.Disabled
            "Shade" => properties.Shade
            "Text" => properties.Text
            "Icon" => properties.Icon
            "IsBusy" => properties.IsBusy
            "BusyText" => properties.BusyText
            
            match properties.Action with
            | Sync action -> attr.callback "Click" (fun (_: MouseEventArgs) -> action())
            | Async action -> attr.task.callback "Click" (fun (_: MouseEventArgs) -> action())
       }
        
    let renderSimple text action =
        
        render <| {
            ButtonProps.defaults with
                Text = text
                Action = ClickAction.Sync action
        }
        
    let renderSimpleAsync text action =
        
        render <| {
            ButtonProps.defaults with
                Text = text
                Action = ClickAction.Async action
        }
