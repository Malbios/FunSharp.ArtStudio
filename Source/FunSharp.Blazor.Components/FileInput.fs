namespace FunSharp.Blazor.Components

open System.Threading.Tasks
open Bolero.Html
open Microsoft.AspNetCore.Components.Forms
open Toolbelt.Blazor.FileDropZone

[<RequireQualifiedAccess>]
module FileInput =
    
    let render allowMultiple (upload: InputFileChangeEventArgs -> unit) (disabled: bool) =
        
        comp<FileDropZone> {
            attr.style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
            
            comp<InputFile> {
                attr.disabled disabled
                attr.multiple allowMultiple
                attr.callback "OnChange" upload
            }
        }
    
    let renderAsync allowMultiple (upload: InputFileChangeEventArgs -> Task) (disabled: bool) =
        
        comp<FileDropZone> {
            attr.style "padding: 2rem; border: 2px solid gray; border-radius: 8px;"
            
            comp<InputFile> {
                attr.disabled disabled
                attr.multiple allowMultiple
                attr.task.callback "OnChange" upload
            }
        }

// TODO: maybe change to Radzen component

// <RadzenStack AlignItems="AlignItems.Center">
//     <RadzenCard class="rz-m-0 rz-m-md-12" Style="width: 100%; max-width: 600px;">
//         <RadzenText TextStyle="TextStyle.H4">Employee: <strong>@(firstEmployee.FirstName + " " + firstEmployee.LastName)</strong></RadzenText>
//         <RadzenFileInput @bind-Value=@firstEmployee.Photo @bind-FileName=@fileName @bind-FileSize=@fileSize TValue="string" Style="width: 100%" 
//             Change=@(args => OnChange(args, "FileInput")) Error=@(args => OnError(args, "FileInput")) InputAttributes="@(new Dictionary<string,object>(){ { "aria-label", "select file" }})"/>
//     </RadzenCard>
// </RadzenStack>
