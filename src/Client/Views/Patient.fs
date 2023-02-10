namespace Views

module Patient =

    open System
    open Feliz
    open Feliz.UseElmish
    open Feliz.Markdown
    open Elmish
    open Shared
    open Types
    open Global
    open Components
    open MaterialUI.Button
    open MaterialUI.Typography
    open MaterialUI.Accordion
    open MaterialUI.FormGroup

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


    let show lang pat =
        match pat with
        | Some p -> p |> Patient.toString lang true
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


    //TODO: Fix Styles
    // let useStyles =
    //     Styles.makeStyles (fun styles theme ->
    //         {|
    //             form =
    //                 styles.create [
    //                     style.display.flex
    //                     style.flexDirection.column
    //                     style.flexGrow 1
    //                 ]
    //             button =
    //                 styles.create [
    //                     style.flexBasis.auto
    //                     style.flexGrow 1
    //                     style.marginTop 10
    //                     style.color "white"
    //                     style.backgroundColor theme.palette.success.light
    //                 ]
    //             show = styles.create [ style.paddingTop 20 ]
    //         |}
    //     )


    [<ReactComponent>]
    let private View
        (input: {| patient: Patient option
                   updatePatient: Patient option -> unit |})
        =
        let lang =
            React.useContext (Global.languageContext)

        let state, dispatch =
            React.useElmish (init, update input.updatePatient, [||])

        //let classes = useStyles ()

        let summary =
            input.patient
            |> function
                | None ->
                    Localization.Terms.``Patient enter patient data``
                    |> Localization.getTerm lang
                | Some p -> p |> Patient.toString lang true
            |> Markdown.render //Utils.Typography.body1

        let inline renderSelect s msg v xs =
            Select.render (Utils.Typography.body1 s) xs v (msg >> dispatch)

        let details =
            Html.div [
                //prop.className classes.form
                prop.children [
                    MaterialFormGroup.create [
                        prop.style [
                            style.display.flex
                            style.paddingBottom 10
                        ]
                        MaterialFormGroup.row true
                        prop.children [
                            [ 0..18 ]
                            |> renderSelect
                                (Localization.Terms.``Patient Years``
                                 |> Localization.getTerm lang)
                                YearChange
                                state.Year
                            [ 0..11 ]
                            |> renderSelect
                                (Localization.Terms.``Patient Months``
                                 |> Localization.getTerm lang)
                                MonthChange
                                state.Month
                            [ 0..4 ]
                            |> renderSelect
                                (Localization.Terms.``Patient Weeks``
                                 |> Localization.getTerm lang)
                                WeekChange
                                state.Week
                            [ 0..6 ]
                            |> renderSelect
                                (Localization.Terms.``Patient Days``
                                 |> Localization.getTerm lang)
                                DayChange
                                state.Day
                            [ 3. .. 100. ] @ [ 105.0..5.0..150.0 ]
                            |> renderSelect
                                $"{(Localization.Terms.``Patient Weight``
                                    |> Localization.getTerm lang)} (kg)"
                                WeightChange
                                state.Weight
                            [ 50. .. 200. ]
                            |> renderSelect
                                $"{(Localization.Terms.``Patient Length``
                                    |> Localization.getTerm lang)} (cm)"
                                HeightChange
                                state.Height
                        ]
                    ]

                    MaterialButton.create [
                        prop.style [ style.flexGrow 1 ]
                        //prop.className classes.button
                        prop.onClick (fun _ -> ClearPatient |> dispatch)
                        MaterialButton.variant "contained"
                        prop.children [
                            MaterialTypography.create [
                                prop.text (
                                    Localization.Terms.``Patient remove patient data``
                                    |> Localization.getTerm lang
                                )
                                MaterialTypography.variant "body1"
                            ]
                        ]
                    ]

                    ]
            ]

        MaterialAccordion.create [
            prop.children [
                MaterialAccordionSummary.create [
                    MaterialAccordionSummary.expandIcon (
                        Fable.MaterialUI.Icons.expandMoreIcon []
                    )

                    prop.children [ summary ]
                ]
                MaterialAccordionDetails.create [
                    prop.children [ details ]
                ]
            ]
        ]


    let render patient updatePatient =
        View(
            {|
                patient = patient
                updatePatient = updatePatient
            |}
        )