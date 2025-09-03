namespace FunSharp.DeviantArt.Manager

open FunSharp.DeviantArt.Api
open FunSharp.DeviantArt.Api.Model
open Microsoft.JSInterop

type IndexedDb(js: IJSRuntime) =
    let mutable moduleRef : IJSObjectReference option = None

    member private this.GetModule() = async {
        match moduleRef with
        | Some m -> return m
        | None ->
            let! m = js.InvokeAsync<IJSObjectReference>("import", "./js/idb.js").AsTask() |> Async.AwaitTask
            moduleRef <- Some m
            return m
    }

    member this.Init(dbName: string, storeNames: string array, ?version: int) = async {
        let! m = this.GetModule()
        let ver = defaultArg version 1
        do! m.InvokeVoidAsync("init", dbName, storeNames, ver).AsTask() |> Async.AwaitTask
    }

    member this.Set(storeName: string, key: string, value: obj) = async {
        let! m = this.GetModule()
        do! m.InvokeVoidAsync("set", storeName, key, value).AsTask() |> Async.AwaitTask
    }

    member this.Get<'T>(storeName: string, key: string) = async {
        let! m = this.GetModule()
        let! v = m.InvokeAsync<'T>("get", storeName, key).AsTask() |> Async.AwaitTask
        return v
    }
    
    member this.GetAll<'T>(storeName: string) = async {
        let! m = this.GetModule()
        let! items = m.InvokeAsync<'T array>("getAll", storeName).AsTask() |> Async.AwaitTask
        return items
    }

    member this.Delete(storeName: string, key: string) = async {
        let! m = this.GetModule()
        do! m.InvokeVoidAsync("del", storeName, key).AsTask() |> Async.AwaitTask
    }

    member this.Keys(storeName: string) = async {
        let! m = this.GetModule()
        let! ks = m.InvokeAsync<string array>("keys", storeName).AsTask() |> Async.AwaitTask
        return ks
    }

    member this.Clear(storeName: string) = async {
        let! m = this.GetModule()
        do! m.InvokeVoidAsync("clear", storeName).AsTask() |> Async.AwaitTask
    }
