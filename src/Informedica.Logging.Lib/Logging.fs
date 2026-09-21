namespace Informedica.Logging.Lib

open System

/// General message types
type IMessage = interface end


type TimeStamp = DateTime


[<RequireQualifiedAccess>]
type Level =
    | Debug
    | Informative
    | Warning
    | Error


type Event =
    {
        TimeStamp: TimeStamp
        Level: Level
        Message: IMessage
    }


type Logger =
    {
        Log: Event -> unit
        /// Whether this logger would consume a message at the given level. Lets
        /// the lazy log API skip building expensive messages that a logger
        /// (the no-op logger, or one filtered above a level) would discard.
        Enabled: Level -> bool
    }


/// General logging module
[<RequireQualifiedAccess>]
module Logging =

    /// Create a message with timestamp and level
    let createMessage level (msg: IMessage) =
        {
            TimeStamp = DateTime.Now
            Level = level
            Message = msg
        }

    /// Log a message with a specific level
    let logWith level (logger: Logger) (msg: IMessage) = msg |> createMessage level |> logger.Log


    /// Log an informative message.
    /// Eager: builds the message at the call site. This is fine because the
    /// logging agent performs the (potentially expensive) formatting and writing
    /// asynchronously. Use logInfoLazy only when building the message itself is
    /// expensive (e.g. rendering a console table).
    let logInfo logger msg = logWith Level.Informative logger msg


    /// Log a warning message. Eager — see logInfo. Prefer logWarningLazy only
    /// when building the message itself is expensive.
    let logWarning logger msg = logWith Level.Warning logger msg


    /// Log a debug message. Eager — see logInfo. Prefer logDebugLazy only
    /// when building the message itself is expensive.
    let logDebug logger msg = logWith Level.Debug logger msg


    /// Log an error message
    let logError logger msg = logWith Level.Error logger msg


    /// A logger that does nothing
    let noOp: Logger =
        {
            Log = ignore
            Enabled = fun _ -> false
        }


    /// Create a logger that uses the given function to process messages
    let create (f: Event -> unit) : Logger =
        {
            Log = f
            Enabled = fun _ -> true
        }


    /// Whether the logger would consume a message at the given level.
    let isEnabled level (logger: Logger) = logger.Enabled level


    /// Log a message built lazily: the thunk (and any expensive work inside it,
    /// e.g. a console table) runs ONLY if the logger would actually consume a
    /// message at this level. Generalises the noOp fast-path to any logger
    /// filtered above the given level.
    let logLazy level (logger: Logger) (mk: unit -> IMessage) =
        if logger.Enabled level then
            mk () |> createMessage level |> logger.Log


    let logInfoLazy logger mk = logLazy Level.Informative logger mk
    let logWarningLazy logger mk = logLazy Level.Warning logger mk
    let logDebugLazy logger mk = logLazy Level.Debug logger mk


    /// Combine multiple loggers into one
    let combine (loggers: Logger list) : Logger =
        { create (fun msg -> loggers |> List.iter (fun logger -> logger.Log msg)) with
            Enabled = fun level -> loggers |> List.exists (fun logger -> logger.Enabled level)
        }


    let levelValue =
        function
        | Level.Debug -> 0
        | Level.Informative -> 1
        | Level.Warning -> 2
        | Level.Error -> 3


    /// Filter messages by level
    let filterByLevel (minLevel: Level) (logger: Logger) : Logger =
        { create (fun msg ->
              if levelValue msg.Level >= levelValue minLevel then
                  logger.Log msg
          ) with
            Enabled = fun level -> levelValue level >= levelValue minLevel && logger.Enabled level
        }


    /// Filter messages by type
    let filterByType<'T when 'T :> IMessage> (logger: Logger) : Logger =
        { create (fun msg ->
              match msg.Message with
              | :? 'T -> logger.Log msg
              | _ -> ()
          ) with
            Enabled = logger.Enabled
        }


/// Message formatter module
[<RequireQualifiedAccess>]
module MessageFormatter =

    /// Create a formatter that handles multiple message types
    let create (formatters: (Type * (IMessage -> string)) list) : IMessage -> string =
        fun msg ->
            let msgType = msg.GetType()
            //printfn $"msgType = {msgType} in {formatters |> List.map (fst >> _.FullName)}"

            // Try to find a formatter where the registered type is assignable from the message type
            formatters
            |> List.tryPick (fun (regType, formatter) ->
                if regType.IsAssignableFrom(msgType) then
                    Some formatter
                else
                    None
            )
            |> Option.map (fun formatter -> formatter msg)
            |> Option.defaultValue $"cannot format: {msg}"


    /// Create a formatter with fallback
    let createWithFallback
        (formatters: (Type * (IMessage -> string)) list)
        (fallback: IMessage -> string)
        : IMessage -> string
        =
        fun msg ->
            let msgType = msg.GetType()

            // Try to find a formatter where the registered type is assignable from the message type
            formatters
            |> List.tryPick (fun (regType, formatter) ->
                if regType.IsAssignableFrom(msgType) then
                    Some formatter
                else
                    None
            )
            |> Option.map (fun formatter -> formatter msg)
            |> Option.defaultWith (fun () -> fallback msg)
