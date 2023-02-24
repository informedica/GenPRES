namespace Components

open Browser.Types

/// Numeric input component that keeps track of min, max and step values.
/// Works well with Safari and Chrome. Has a problem with Firefox.
/// The problem is that in Firefox, the numeric input box accepts non numerical entries but totally
/// ignores this, so, also doesn't trigger any events.
module NumericInput =
    open System

    open Elmish
    open Elmish.React
    open Fable.React
    open Fable.React.Props
    open Fetch.Types
    open Thoth.Fetch
    open Thoth.Json
    open Thoth.Elmish
    open Feliz
    open Feliz.UseElmish
    open MaterialUI5
    open Fable.Core.JsInterop
    open Browser



    /// The debouncer keeps tracks of the bounce time
    /// before the UserInput is actually used.
    type State =
        {
            Debouncer: Debouncer.State
            Error: bool
            UserInput: string
        }


    type Msg =
        | DebouncerSelfMsg of Debouncer.SelfMessage<Msg>
        | ChangeValue of string
        | EndOfInput


    type Props =
        {| min: float option
           max: float option
           step: float
           label: string
           adorn: string
           dispatch: string -> unit |}


    let private init () =
        {
            Debouncer = Debouncer.create ()
            Error = false
            UserInput = ""
        },
        Cmd.none


    let private update (props: Props) msg state =
        let parse = Shared.Utils.tryParseFloat

        let isErr (s: string) =
            if s.Trim() = "" then
                true
            else
                match s |> parse, props.min, props.max with
                | None, _, _ -> false
                | Some _, None, None -> true
                | Some v, Some min, None -> v >= min
                | Some v, None, Some max -> v <= max
                | Some v, Some min, Some max -> v >= min && v <= max
            |> not

        match msg with
        | ChangeValue s ->
            let debouncerModel, debouncerCmd =
                state.Debouncer
                |> Debouncer.bounce
                    (TimeSpan.FromSeconds 0.5)
                    "end-of-input"
                    EndOfInput

            { state with
                Error = s |> isErr
                UserInput =
                    if s = "" then
                        s
                    else
                        match s |> parse with
                        | Some _ -> s
                        | None -> state.UserInput
                Debouncer = debouncerModel
            },

            Cmd.map DebouncerSelfMsg debouncerCmd

        | DebouncerSelfMsg debouncerMsg ->
            let debouncerModel, debouncerCmd =
                Debouncer.update debouncerMsg state.Debouncer

            { state with
                Debouncer = debouncerModel
            },
            debouncerCmd
        // End of user input has reached so know actually dispatch the
        // input to the dispatch function
        | EndOfInput ->
            let state =
                { state with
                    Error = state.UserInput |> isErr
                }

            state, Cmd.ofSub (fun _ -> state.UserInput |> props.dispatch)


    let useStyles err =
        Styles.makeStyles (fun styles theme ->
            {|
                field =
                    styles.create [
                        style.minWidth (theme.spacing 14)
                        style.marginTop (theme.spacing 1)
                    ]
                input =
                    styles.create [
                        if not err then
                            style.color (theme.palette.primary.main)
                        else
                            style.color (theme.palette.error.main)
                    ]
                label =
                    styles.create [
                        style.fontSize (theme.typography.fontSize - 15.)
                        style.paddingRight (theme.spacing 2)
                    ]
            |}
        )


    let defaultProps: Props =
        {|
            min = None
            max = None
            step = 1.
            label = ""
            adorn = ""
            dispatch = (fun (s: string) -> ())
        |}


    let private comp =
        React.functionComponent (
            "numericinput",
            fun (props: Props) ->
                let state, dispatch =
                    React.useElmish (init, update props, [||])

                let classes = (useStyles state.Error) ()


                Mui.textField [
                    prop.className classes.field
                    textField.error state.Error
                    textField.label (
                        Mui.typography [

                            typography.variant.body2
                            typography.children [ props.label ]
                        ]
                    )

                    textField.value state.UserInput
                    textField.onChange (ChangeValue >> dispatch)

                    // dirty fix to disable number field typ in FF
                    let isFF =
                        navigator.userAgent.ToLower().Contains("firefox")

                    if not isFF then
                        textField.type' "number"

                    textField.size.small

                    textField.InputProps [
                        input.inputProps [
                            prop.step props.step
                            match props.min with
                            | Some m -> prop.min m
                            | None -> prop.min 0.
                            match props.max with
                            | Some m -> prop.max m
                            | None -> ()
                        ]

                        // sets the color of the input value
                        prop.className classes.input
                        // adds a unit (adornment) to a value
                        input.endAdornment (
                            Mui.inputAdornment [
                                inputAdornment.position.end'
                                inputAdornment.children [
                                    Mui.typography [
                                        typography.color.textSecondary
                                        typography.variant.body2
                                        typography.children [ props.adorn ]
                                    ]
                                ]
                            ]
                        )
                    ]
                ]
        )


    let render label adorn dispatch =
        comp (
            {| defaultProps with
                label = label
                adorn = adorn
                dispatch = dispatch
            |}
        )


    let renderWithProps props = comp (props)