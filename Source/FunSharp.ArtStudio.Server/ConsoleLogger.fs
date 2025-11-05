namespace FunSharp.ArtStudio.Server

open System
open Microsoft.Extensions.Logging

type ConsoleLogger<'T>() =
    
    interface ILogger<'T> with
    
        member _.IsEnabled(_logLevel) = true

        member _.BeginScope(_state: 'TState) = 
            { new IDisposable with member _.Dispose() = () }

        member _.Log(logLevel, _eventId, state: 'TState, ex: exn, formatter: Func<'TState, exn, string>) =
            let message = 
                if isNull formatter then state.ToString()
                else formatter.Invoke(state, ex)
            let color =
                match logLevel with
                | LogLevel.Critical -> ConsoleColor.Red
                | LogLevel.Error -> ConsoleColor.DarkRed
                | LogLevel.Warning -> ConsoleColor.Yellow
                | LogLevel.Information -> ConsoleColor.Gray
                | LogLevel.Debug -> ConsoleColor.DarkGray
                | LogLevel.Trace -> ConsoleColor.DarkCyan
                | _ -> ConsoleColor.White

            let old = Console.ForegroundColor
            Console.ForegroundColor <- color
            printfn "[%s] [%s] %s" (DateTime.Now.ToString("HH:mm:ss")) (logLevel.ToString()) message
            Console.ForegroundColor <- old
