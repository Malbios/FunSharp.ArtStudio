namespace FunSharp.ArtStudio.Client.Components

open System.IO
open FunSharp.Blazor.Components
open FunSharp.ArtStudio.Client.Model
open Microsoft.AspNetCore.Components
open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components.Forms
open Radzen
open Radzen.Blazor

type ImageUploadDialog() =
    inherit Component()
        
    let maxSize = 1024L * 1024L * 100L // 100 MB
    
    let mutable isBusy = false
    let mutable uploadedFile : Image|null = null
    
    let processUploadedFile (file: IBrowserFile) = task {
        isBusy <- true
        
        use stream = file.OpenReadStream(maxAllowedSize = maxSize)
        use ms = new MemoryStream()
        
        do! stream.CopyToAsync(ms)
        
        uploadedFile <- {
            Name = file.Name
            ContentType = file.ContentType
            Content = ms.ToArray()
        }
        
        isBusy <- false
    }

    [<Inject>]
    member val DialogService = Unchecked.defaultof<DialogService> with get, set

    override this.Render() =
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            
            FileInput.renderAsync false (fun (args: InputFileChangeEventArgs) -> processUploadedFile args.File) false
            
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                
                Button.render <| {
                    ButtonProps.defaults with
                        Text = "Ok"
                        Action = ClickAction.Sync <| fun () -> this.DialogService.Close(uploadedFile)
                        IsBusy = isBusy
                }
                
                Button.renderSimple "Cancel" <| fun () -> this.DialogService.Close(null)
            }
        }
        
    static member OpenAsync(dialogService: DialogService, title: string) =
        
        dialogService.OpenAsync<ImageUploadDialog>(title)
