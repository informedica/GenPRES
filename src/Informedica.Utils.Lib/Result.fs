namespace Informedica.Utils.Lib


type ValidatedResult<'T, 'Msg> = Result<'T * 'Msg list, 'Msg list>


[<RequireQualifiedAccess>]
module Result =

    let get = function
        | Ok r -> r
        | Error _ -> failwith "cannot get result from Error"

    module Tests =

        open Swensen.Unquote

        // Test get
        let testGet () =
            let result = Ok 1 in
            let actual = get result in
            let expected = 1 in
            test <@ actual = expected @>

        // Test get error
        let testGetError () =
            Assertions.raises<System.Exception> <@ Error "error" |> get @>

        // Test all
        let testAll () =
            testGet ()
            testGetError ()


[<RequireQualifiedAccess>]
module ValidatedResult =

    // Creation functions
    let createOkWithMsgs msgs x : ValidatedResult<_, _> = (x, msgs) |> Ok

    let createOkNoMsgs x = createOkWithMsgs [] x

    let createOk x msgs = createOkWithMsgs msgs x

    let createError msgs = Error msgs

    // Extraction functions
    let get = function
        | Ok (r, _) -> r
        | Error _ -> failwith "cannot get result from Error"

    let getValue = function
        | Ok (value, _) -> Some value
        | Error _ -> None

    let getMessages = function
        | Ok (_, msgs) -> msgs
        | Error msgs -> msgs

    // Basic functions
    let isOk = function
        | Ok _ -> true
        | Error _ -> false

    let isError = function
        | Ok _ -> false
        | Error _ -> true

    // Map functions
    let map f = function
        | Ok (value, msgs) -> Ok (f value, msgs)
        | Error msgs -> Error msgs

    let mapError f = function
        | Ok (value, msgs) -> Ok (value, msgs)
        | Error msgs -> Error (f msgs)

    let mapMessages f = function
        | Ok (value, msgs) -> Ok (value, f msgs)
        | Error msgs -> Error (f msgs)

    // Bind functions
    let bind f = function
        | Ok (value, msgs1) ->
            match f value with
            | Ok (newValue, msgs2) -> Ok (newValue, msgs1 @ msgs2)
            | Error msgs2 -> Error (msgs1 @ msgs2)
        | Error msgs -> Error msgs

    let bindError f = function
        | Ok (value, msgs) -> Ok (value, msgs)
        | Error msgs -> f msgs

    // Apply functions (for applicative style)
    let apply fResult xResult =
        match fResult, xResult with
        | Ok (f, msgs1), Ok (x, msgs2) -> Ok (f x, msgs1 @ msgs2)
        | Ok (_, msgs1), Error msgs2 -> Error (msgs1 @ msgs2)
        | Error msgs1, Ok (_, msgs2) -> Error (msgs1 @ msgs2)
        | Error msgs1, Error msgs2 -> Error (msgs1 @ msgs2)

    // Message manipulation
    let addMessage msg = function
        | Ok (value, msgs) -> Ok (value, msg :: msgs)
        | Error msgs -> Error (msg :: msgs)

    let addMessages newMsgs = function
        | Ok (value, msgs) -> Ok (value, msgs @ newMsgs)
        | Error msgs -> Error (msgs @ newMsgs)

    let prependMessage msg = function
        | Ok (value, msgs) -> Ok (value, msg :: msgs)
        | Error msgs -> Error (msg :: msgs)

    let prependMessages newMsgs = function
        | Ok (value, msgs) -> Ok (value, newMsgs @ msgs)
        | Error msgs -> Error (newMsgs @ msgs)

    // Fold and iteration
    let fold onOk onError = function
        | Ok (value, msgs) -> onOk value msgs
        | Error msgs -> onError msgs

    let iter action = function
        | Ok (value, _) -> action value
        | Error _ -> ()

    let iterError action = function
        | Ok _ -> ()
        | Error msgs -> action msgs

    // Conversion functions
    let toResult = function
        | Ok (value, _) -> Ok value
        | Error msgs -> Error msgs

    let fromResult msgs = function
        | Ok value -> Ok (value, msgs)
        | Error error -> Error [error]

    let toOption = function
        | Ok (value, _) -> Some value
        | Error _ -> None

    // Collection functions
    let sequence (results: ValidatedResult<'T, 'Msg> list) : ValidatedResult<'T list, 'Msg> =
        let rec loop acc msgs = function
            | [] -> Ok (List.rev acc, msgs)
            | Ok (value, newMsgs) :: rest -> 
                loop (value :: acc) (msgs @ newMsgs) rest
            | Error errorMsgs :: _ -> 
                Error (msgs @ errorMsgs)
        loop [] [] results

    let traverse f list =
        list
        |> List.map f
        |> sequence

    // Choice functions
    let orElse alternative = function
        | Ok (value, msgs) -> Ok (value, msgs)
        | Error _ -> alternative

    let orElseWith f = function
        | Ok (value, msgs) -> Ok (value, msgs)
        | Error msgs -> f msgs

    // Default value functions
    let defaultValue defaultVal = function
        | Ok (value, _) -> value
        | Error _ -> defaultVal

    let defaultWith f = function
        | Ok (value, _) -> value
        | Error msgs -> f msgs

    // Operators
    let (>>=) x f = bind f x
    let (>>|) x f = map f x
    let (<*>) f x = apply f x

    // Computation expression
    type ValidatedResultBuilder() =
        member _.Return(x) = createOkNoMsgs x
        member _.ReturnFrom(x) = x
        member _.Bind(x, f) = bind f x
        member _.Zero() = createOkNoMsgs ()
        
        member _.Combine(r1, r2) =
            match r1 with
            | Ok ((), msgs1) ->
                match r2 with
                | Ok (value, msgs2) -> Ok (value, msgs1 @ msgs2)
                | Error msgs2 -> Error (msgs1 @ msgs2)
            | Error msgs1 ->
                match r2 with
                | Ok (_, msgs2) -> Error (msgs1 @ msgs2)
                | Error msgs2 -> Error (msgs1 @ msgs2)

        member _.Delay(f) = f
        member _.Run(f) = f()

    let validatedResult = ValidatedResultBuilder()

    // Utility functions for common patterns
    let ofOption msg = function
        | Some value -> createOkNoMsgs value
        | None -> createError [msg]

    let ofChoice onLeft onRight = function
        | Choice1Of2 value -> onLeft value
        | Choice2Of2 error -> onRight error

    let catch f x =
        try
            f x |> createOkNoMsgs
        with
        | ex -> createError [ex.Message]

    let tee f result =
        match result with
        | Ok (value, msgs) -> 
            f value
            Ok (value, msgs)
        | Error msgs -> Error msgs

    let teeError f result =
        match result with
        | Ok (value, msgs) -> Ok (value, msgs)
        | Error msgs ->
            f msgs
            Error msgs

    // Validation helpers
    let ensure predicate msg value =
        if predicate value then
            createOkNoMsgs value
        else
            createError [msg]

    let requireSome msg = function
        | Some value -> createOkNoMsgs value
        | None -> createError [msg]

    let requireEmpty msg = function
        | [] -> createOkNoMsgs ()
        | items -> createError [msg]

    let requireNotEmpty msg = function
        | [] -> createError [msg]
        | items -> createOkNoMsgs items

    // Tests module
    module Tests =
        open Swensen.Unquote

        let testCreateOk () =
            let result = createOk 42 ["info"]
            test <@ result = Ok (42, ["info"]) @>

        let testMap () =
            let result = createOk 5 ["msg"] |> map ((*) 2)
            test <@ result = Ok (10, ["msg"]) @>

        let testBind () =
            let f x = createOk (x * 2) ["doubled"]
            let result = createOk 5 ["original"] |> bind f
            test <@ result = Ok (10, ["original"; "doubled"]) @>

        let testBindError () =
            let f x = createError ["error"]
            let result = createOk 5 ["original"] |> bind f
            test <@ result = Error ["original"; "error"] @>

        let testSequence () =
            let results = [
                createOk 1 ["msg1"]
                createOk 2 ["msg2"]
                createOk 3 ["msg3"]
            ]
            let result = sequence results
            test <@ result = Ok ([1; 2; 3], ["msg1"; "msg2"; "msg3"]) @>

        let testComputationExpression () =
            let result = validatedResult {
                let! x = createOk 5 ["got 5"]
                let! y = createOk 3 ["got 3"]
                return x + y
            }
            test <@ result = Ok (8, ["got 5"; "got 3"]) @>

        let testAll () =
            testCreateOk ()
            testMap ()
            testBind ()
            testBindError ()
            testSequence ()
            testComputationExpression ()
