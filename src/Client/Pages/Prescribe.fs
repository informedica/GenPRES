namespace Pages


module Prescribe =

    open Feliz
    open Feliz.UseElmish
    open Elmish
    open Feliz.MaterialUI
    open Shared
    open Global
    open Types
    open Views
    open Utils


    type State =
        {
            Dialog: string list
            Indication: string option
            Medication: string option
            Route: string option
        }


    type Msg =
        | RowClick of int * string list
        | CloseDialog
        | IndicationChange of string
        | MedicationChange of string
        | RouteChange of string


    let init (scenarios: Deferred<ScenarioResult>) =
        let state =
            match scenarios with
            | Resolved sc ->
                {
                    Dialog = []
                    Indication = sc.Indication
                    Medication = sc.Medication
                    Route = sc.Route
                }
            | _ ->
                {
                    Dialog = []
                    Indication = None
                    Medication = None
                    Route = None
                }
        state, Cmd.none


    let update
        (scenarios: Deferred<ScenarioResult>)
        updateScenarios
        (msg: Msg)
        state
        =
        match msg with
        | RowClick (i, xs) ->
            Logging.log "rowclick:" i
            { state with Dialog = xs }, Cmd.none
        | CloseDialog -> { state with Dialog = [] }, Cmd.none
        | IndicationChange s ->
            let cmd =
                match scenarios with
                | Resolved sc ->
                    Cmd.ofSub (fun _ ->
                        { sc with Indication = Some s } |> updateScenarios
                    )
                | _ -> Cmd.none

            { state with Indication = Some s }, cmd
        | MedicationChange s ->
            let cmd =
                match scenarios with
                | Resolved sc ->
                    Cmd.ofSub (fun _ ->
                        { sc with Medication = Some s } |> updateScenarios
                    )
                | _ -> Cmd.none

            { state with Medication = Some s }, cmd
        | RouteChange s ->
            let cmd =
                match scenarios with
                | Resolved sc ->
                    Cmd.ofSub (fun _ ->
                        { sc with Route = Some s } |> updateScenarios
                    )
                | _ -> Cmd.none

            { state with Route = Some s }, cmd

    let useStyles =
        Styles.makeStyles (fun styles theme ->
            {|
                container =
                    styles.create [
                        style.boxSizing.borderBox
                    ]
                mainview =
                    styles.create [
                        style.display.flex
                        style.overflow.hidden
                        style.flexDirection.column
                        style.marginLeft 20
                        style.marginRight 20
                    ]
                patientpanel =
                    styles.create [
                        style.marginTop 70
                        style.top 0
                    ]
                patientdetails =
                    styles.create [
                        style.display.flex
                        style.flexDirection.row
                    ]
            |}
        )


    [<ReactComponent>]
    let View
        (input: {| scenarios: Deferred<ScenarioResult>
                   updateScenarios: ScenarioResult -> unit |})
        =
        let lang =
            React.useContext (Global.languageContext)

        let state, dispatch =
            React.useElmish (
                init input.scenarios,
                update input.scenarios input.updateScenarios,
                [| box input.scenarios |]
            )

        let classes_ = useStyles ()

        let inds, meds, rts, scs =
            match input.scenarios with
            | Resolved sc ->
                sc.Indications, sc.Medications, sc.Routes, sc.Scenarios
            | _ -> [], [], [], []


        let inline renderSelect s msg v xs =
            Components.Select.render (Typography.body1 s) xs v (msg >> dispatch)

        let scenarioList =
                scs
                |> List.map (fun sc ->
                    Mui.listItem [
                        sc |> Typography.body1
                    ]
                )
                |> Mui.list

        Html.div [
            prop.id "prescribe-div"
            prop.children [
                renderSelect "Indicaties" IndicationChange state.Indication inds

                renderSelect "Medicatie" MedicationChange state.Medication meds

                renderSelect "Routes" RouteChange state.Route rts

                scenarioList
            ]
        ]


    let render scenarios updateScenarios =
        View(
            {|
                scenarios = scenarios
                updateScenarios = updateScenarios
            |}
        )