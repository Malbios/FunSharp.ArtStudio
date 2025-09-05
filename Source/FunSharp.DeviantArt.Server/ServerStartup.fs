namespace FunSharp.DeviantArt.Server

open FunSharp.Data
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.RequestErrors
open Suave.Writers
open FunSharp.Common
open FunSharp.DeviantArt.Api

module ServerStartup =

    let secrets = Secrets.load ()
    let authPersistence = Persistence.AuthenticationPersistence()
    let dataPersistence : Abstraction.IPersistence = PickledPersistence("persistence.db")
    let client = Client(authPersistence, secrets.client_id, secrets.client_secret)

    let allowCors: WebPart =
        setHeader "Access-Control-Allow-Origin" "*"
        >=> setHeader "Access-Control-Allow-Headers" "Content-Type"
        >=> setHeader "Access-Control-Allow-Methods" "GET, POST, OPTIONS"

    let corsPreflight: WebPart =
        pathRegex ".*" >=> OPTIONS >=> allowCors >=> OK "CORS preflight"

    let usernameHandler: WebPart =
        fun ctx -> async {
            let! username =
                client.WhoAmI()
                |> AsyncResult.getOrFail
                |> Async.map _.username
                
            return! {| username = username |} |> Helpers.jsonResponse <| ctx
        }
        
    let uploadHandler: WebPart =
        fun ctx -> async {
            match ctx.request.files with
            | [] -> return! BAD_REQUEST "No files uploaded" ctx

            | [ file ] ->
                let key = file.fileName

                return! "ok" |> Helpers.jsonResponse <| ctx

            | _ -> return! BAD_REQUEST "Upload of multiple files is currently not supported" ctx
        }

    let stashHandler: WebPart =
        fun ctx -> async {
            match ctx.request.files with
            | [] -> return! BAD_REQUEST "No files uploaded" ctx

            | [ file ] ->
                let title =
                    match ctx.request.fieldData "title" with
                    | Choice1Of2 v -> v
                    | Choice2Of2 v -> failwith $"{v}"

                let! response = Helpers.submitToStash client title file

                return! response |> Helpers.jsonResponse <| ctx

            | _ -> return! BAD_REQUEST "Multiple files not supported" ctx
        }

    [<EntryPoint>]
    let main _ =

        let config =
            { defaultConfig with
                bindings = [ HttpBinding.createSimple HTTP "0.0.0.0" 5123 ] }

        let apiBase = "/api/v1"

        allowCors
        >=> choose
                [ corsPreflight

                  GET >=> path $"{apiBase}/username" >=> usernameHandler

                  POST >=> path $"{apiBase}/upload" >=> uploadHandler

                  POST >=> path $"{apiBase}/stash" >=> stashHandler

                  NOT_FOUND "Unknown route" ]
        |> startWebServer config

        0

// get stash url (maybe as part of the response instead)

// local/inspiration
// local/prompt
// local/deviation
// stash
// publish
