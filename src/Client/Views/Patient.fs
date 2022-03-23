namespace Views

module Patient =

    open System
    open Feliz.MaterialUI
    open Feliz
    open Feliz.UseElmish
    open Feliz.Markdown
    open Elmish
    open Shared
    open Types
    open Global
    open Components

    type State =
        {
            Year: int option
            Month: int option
            Week: int option
            Day: int option
            Weight: float option
            Height: float option
        }


    type Msg =
        | ClearPatient
        | YearChange of int
        | MonthChange of int
        | WeekChange of int
        | DayChange of int
        | WeightChange of float
        | HeightChange of float


    let newState =
        {
            Year = None
            Month = None
            Week = None
            Day = None
            Weight = None
            Height = None
        }


    let init () = newState, Cmd.none


    let show pat =
        match pat with
        | Some p -> p |> Patient.toString true
        | None -> ""


    let update updatePatient msg state =

        match msg with
        | ClearPatient -> newState
        | YearChange n -> { state with Year = Some n }
        | MonthChange n -> { state with Month = Some n }
        | WeekChange n -> { state with Week = Some n }
        | DayChange n -> { state with Day = Some n }
        | WeightChange n -> { state with Weight = Some n }
        | HeightChange n -> { state with Height = Some n }
        |> fun state ->
            state,
            Cmd.ofSub (fun _ ->
                Patient.create
                    state.Year
                    state.Month
                    state.Week
                    state.Day
                    state.Weight
                    state.Height
                |> updatePatient
            )



    let useStyles =
        Styles.makeStyles (fun styles theme ->
            {|
                form =
                    styles.create [
                        style.display.flex
                        style.flexDirection.column
                        style.flexGrow 1
                    ]
                button =
                    styles.create [
                        style.flexBasis.auto
                        style.flexGrow 1
                        style.marginTop 10
                        style.color "white"
                        style.backgroundColor theme.palette.success.light
                    ]
                show = styles.create [ style.paddingTop 20 ]
            |}
        )


    [<ReactComponent>]
    let private View
        (input: {| patient: Patient option
                   updatePatient: Patient option -> unit |})
        =
        let state, dispatch =
            React.useElmish (init, update input.updatePatient, [||])

        let classes = useStyles ()

        let summary =
            input.patient
            |> function
                | None -> "Voer patient gegevens in ..."
                | Some p -> p |> Patient.toString true
            |> Markdown.render //Utils.Typography.body1

        let inline renderSelect s msg v xs =
            Select.render (Utils.Typography.body1 s) xs v (msg >> dispatch)

        let details =
            Html.div [
                prop.className classes.form
                prop.children [
                    Mui.formGroup [
                        prop.style [
                            style.display.flex
                            style.paddingBottom 10
                        ]
                        formGroup.row true
                        formGroup.children [
                            [ 0..18 ]
                            |> renderSelect "Jaar" YearChange state.Year
                            [ 0..11 ]
                            |> renderSelect "Maand" MonthChange state.Month
                            [ 0..4 ]
                            |> renderSelect "Week" WeekChange state.Week
                            [ 0..6 ] |> renderSelect "Dag" DayChange state.Day
                            [ 3. .. 100. ]
                            |> renderSelect "Gewicht" WeightChange state.Weight
                            [ 50. .. 200. ]
                            |> renderSelect "Lengte" HeightChange state.Height
                        ]
                    ]

                    Mui.button [
                        prop.style [ style.flexGrow 1 ]
                        prop.className classes.button
                        prop.onClick (fun _ -> ClearPatient |> dispatch)
                        button.variant.contained
                        button.children [
                            Mui.typography [
                                prop.text "verwijder"
                                typography.variant.body1
                            ]
                        ]
                    ]

                    ]
            ]

        Mui.accordion [
            Mui.accordionSummary [
                accordionSummary.expandIcon (
                    Fable.MaterialUI.Icons.expandMoreIcon []
                )

                accordionSummary.children [ summary ]
            ]
            Mui.accordionDetails [
                accordionDetails.children [ details ]
            ]
        ]


    let render patient updatePatient =
        View(
            {|
                patient = patient
                updatePatient = updatePatient
            |}
        )