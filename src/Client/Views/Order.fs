namespace Views



module Order =

    open Fable.Core
    open Fable.React
    open Feliz
    open Shared.Types
    open Shared.Models
    open Shared.Models.Order
    open Shared
    open Elmish
    open Utils
    open FSharp.Core


    module private Elmish =


        type State =
            {
                Order : Order option
                SelectedComponent : string option
                SelectedItem : string option
            }

        type Msg =
            | ChangeComponent of string option
            | ChangeItem of string option
            | ChangeFrequency of string option
            | ChangeTime of string option
            | ChangeSubstanceDoseQuantity of string option
            | ChangeSubstancePerTime of string option
            | ChangeSubstancePerTimeAdjust of string option
            | ChangeSubstanceRate of string option
            | ChangeSubstanceRateAdjust of string option
            | ChangeSubstanceComponentConcentration of string option
            | ChangeOrderableDoseQuantity of string option
            | ChangeOrderableDoseRate of string option
            | UpdateOrder of Order


        let init (pr : Deferred<Types.PrescriptionResult>) =
            let ord, cmp, itm =
                match pr with
                | Resolved pr ->
                    match pr.Scenarios with
                    | [| sc |] ->

                        let ord =
                            sc.Order
                            |> OrderState.getOrder
                        let cmp = sc.Component
                        let itm = sc.Item

                        match ord.Orderable.Components with
                        | [||] -> Some ord, None, None
                        | _ ->
                            ord.Orderable.Components
                            |> Array.tryFind (fun c ->
                                cmp.IsNone ||
                                c.Name = cmp.Value
                            )
                            |> Option.map (fun c ->
                                // only use substances that are not additional
                                let substs =
                                    c.Items
                                    |> Array.filter (_.IsAdditional >> not)

                                if substs |> Array.isEmpty then
                                    Some ord,
                                    Some c.Name,
                                    None
                                else
                                    let s =
                                        substs
                                        |> Array.tryFind (fun i -> i.Name |> Some = itm)
                                        |> Option.map (fun s -> s.Name)
                                        |> Option.defaultValue (substs[0].Name)
                                        |> Some
                                    Some ord,
                                    Some c.Name,
                                    s
                            )
                            |> Option.defaultValue (Some ord, None, None)

                    | _ -> None, None, None

                | _ -> None, None, None

            {
                SelectedComponent = cmp
                SelectedItem = itm
                Order = ord
            }
            , Cmd.none


        let update
            updateScenarioResult
            (msg: Msg)
            (state : State) : State * Cmd<Msg>
            =
            let setVu s (vu : Types.ValueUnit option) =
                match vu with
                | Some vu ->
                    { vu with
                        Value =
                            vu.Value
                            |> Array.tryFind (fun (v, _) ->
                                let b = v = (s |> Option.defaultValue "")
                                if not b then Logging.warning "couldn't find" s
                                b
                            )
                            |> Option.map Array.singleton
                            |> Option.defaultValue vu.Value
                    } |> Some
                | None -> None

            match msg with

            | UpdateOrder ord ->

                OrderLoader.create state.SelectedComponent state.SelectedItem ord
                |> updateScenarioResult

                { state with
                    Order = None
                }
                , Cmd.none

            | ChangeComponent cmp ->
                match cmp with
                | None -> state, Cmd.none
                | Some _ ->
                    { state with
                        SelectedComponent = cmp
                        SelectedItem =
                            if state.SelectedComponent = cmp then state.SelectedItem
                            else None
                    }, Cmd.none

            | ChangeItem itm ->
                match itm with
                | None -> state, Cmd.none
                | Some _ -> { state with SelectedItem = itm }, Cmd.none

            | ChangeFrequency s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Prescription =
                                { ord.Prescription with
                                    Frequency =
                                        { ord.Prescription.Frequency with
                                            Variable =
                                                { ord.Prescription.Frequency.Variable with
                                                    Vals = ord.Prescription.Frequency.Variable.Vals |> setVu s
                                                }
                                        }
                                }
                        }
                        |> UpdateOrder
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeTime s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Prescription =
                                { ord.Prescription with
                                    Time =
                                        { ord.Prescription.Time with
                                            Variable =
                                                { ord.Prescription.Time.Variable with
                                                    Vals = ord.Prescription.Time.Variable.Vals |> setVu s
                                                }
                                        }
                                }
                        }
                        |> UpdateOrder
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceDoseQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    Dose =
                                                                        { itm.Dose with
                                                                            Quantity =
                                                                                { itm.Dose.Quantity with
                                                                                    Variable =
                                                                                        { itm.Dose.Quantity.Variable with
                                                                                            Vals = itm.Dose.Quantity.Variable.Vals |> setVu s
                                                                                        }
                                                                                }

                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrder
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstancePerTime s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    Dose =
                                                                        { itm.Dose with
                                                                            PerTime =
                                                                                { itm.Dose.PerTime with
                                                                                    Variable =
                                                                                        { itm.Dose.PerTime.Variable with
                                                                                            Vals = itm.Dose.PerTime.Variable.Vals |> setVu s
                                                                                        }
                                                                                }

                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrder
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstancePerTimeAdjust s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    Dose =
                                                                        { itm.Dose with
                                                                            PerTimeAdjust =
                                                                                { itm.Dose.PerTimeAdjust with
                                                                                    Variable =
                                                                                        { itm.Dose.PerTimeAdjust.Variable with
                                                                                            Vals = itm.Dose.PerTimeAdjust.Variable.Vals |> setVu s
                                                                                        }
                                                                                }

                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrder
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceRate s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    Dose =
                                                                        { itm.Dose with
                                                                            Rate =
                                                                                { itm.Dose.Rate with
                                                                                    Variable =
                                                                                        { itm.Dose.Rate.Variable with
                                                                                            Vals = itm.Dose.Rate.Variable.Vals |> setVu s
                                                                                        }
                                                                                }

                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrder
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceRateAdjust s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    Dose =
                                                                        { itm.Dose with
                                                                            RateAdjust =
                                                                                { itm.Dose.RateAdjust with
                                                                                    Variable =
                                                                                        { itm.Dose.RateAdjust.Variable with
                                                                                            Vals = itm.Dose.RateAdjust.Variable.Vals |> setVu s
                                                                                        }
                                                                                }

                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrder
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceComponentConcentration s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    ComponentConcentration =
                                                                        { itm.ComponentConcentration with
                                                                            Variable =
                                                                                { itm.ComponentConcentration.Variable with
                                                                                    Vals = itm.ComponentConcentration.Variable.Vals |> setVu s
                                                                                }

                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrder
                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableDoseQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Dose =
                                        { ord.Orderable.Dose with
                                            Quantity =
                                                { ord.Orderable.Dose.Quantity with
                                                    Variable =
                                                        { ord.Orderable.Dose.Quantity.Variable with
                                                            Vals = ord.Orderable.Dose.Quantity.Variable.Vals |> setVu s
                                                        }
                                                }

                                        }
                                }

                        }
                        |> UpdateOrder
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableDoseRate s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Dose =
                                        { ord.Orderable.Dose with
                                            Rate =
                                                { ord.Orderable.Dose.Rate with
                                                    Variable =
                                                        { ord.Orderable.Dose.Rate.Variable with
                                                            Vals = ord.Orderable.Dose.Rate.Variable.Vals |> setVu s
                                                        }
                                                }

                                        }
                                }

                        }
                        |> UpdateOrder
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

        let showOrderName (ord : Order option) =
            match ord with
            | Some ord ->
                $"{ord.Orderable.Name} {ord.Orderable.Components[0].Shape}"
            | None -> "order is loading ..."


    open Elmish
    open Shared


    [<JSX.Component>]
    let View (props:
        {|
            prescriptionResult: Deferred<Types.PrescriptionResult>
            updatePrescriptionResult: Types.PrescriptionResult -> unit
            closeOrder : unit -> unit
            localizationTerms : Deferred<string [] []>
        |}) =

        let context = React.useContext Global.context
        let lang = context.Localization

        let getTerm defVal term =
            props.localizationTerms
            |> Deferred.map (fun terms ->
                Localization.getTerm terms lang term
                |> Option.defaultValue defVal
            )
            |> Deferred.defaultValue defVal

        let useAdjust =
            match props.prescriptionResult with
            | Resolved pr ->
                pr.Scenarios
                |> Array.tryExactlyOne
                |> Option.map (fun sc -> sc.UseAdjust)
                |> Option.defaultValue false
            | _ -> false

        let updateScenarioResult (ol : OrderLoader) =
            match props.prescriptionResult with
            | Resolved pr ->
                { pr with
                    Scenarios =
                        pr.Scenarios
                        |> Array.map (fun sc ->
                            if (sc.Order |> OrderState.getOrder).Id <> ol.Order.Id then sc
                            else
                                let state o =
                                    match sc.Order with
                                    | Constrained _ -> o |> Constrained
                                    | Solved _      -> o |> Solved
                                    | Calculated _  -> o |> Calculated
                                {
                                    sc with
                                        Component = ol.Component
                                        Item = ol.Item
                                        Order =
                                            ol.Order
                                            |> state
                                }
                        )
                }
                |> props.updatePrescriptionResult
            | _ -> ()

        let state, dispatch =
            React.useElmish (
                init props.prescriptionResult,
                update updateScenarioResult,
                [| box props.prescriptionResult |]
            )

        let itms =
            match state.Order with
            | Some ord ->
                ord.Orderable.Components
                // only use the main component for dosing
                |> Array.tryFind(fun cmp ->
                    state.SelectedComponent.IsNone ||
                    state.SelectedComponent.Value = cmp.Name
                )
                |> Option.map (fun cmp ->
                    // filter out additional items, they are not used for dosing
                    cmp.Items
                    |> Array.filter (_.IsAdditional >> not)
                )
                |> Option.defaultValue [||]
            | _ -> [||]

        let substIndx =
            itms
            |> Array.tryFindIndex (fun i ->
                state.SelectedItem
                |> Option.map ((=) i.Name)
                |> Option.defaultValue false
            )
            |> function
            | None -> Some 0
            | Some i -> Some i

        let select isLoading lbl selected dispatch xs =
            if xs |> Array.isEmpty then
                JSX.jsx $"<></>"
            else
                Components.SimpleSelect.View({|
                    updateSelected = dispatch
                    label = lbl
                    selected =
                        if xs |> Array.length = 1 then xs[0] |> fst |> Some
                        else selected
                    values = xs
                    isLoading = isLoading
                |})

        let progress =
            match props.prescriptionResult with
            | Resolved _ -> JSX.jsx $"<></>"
            | _ ->
                JSX.jsx
                    $"""
                import CircularProgress from '@mui/material/CircularProgress';

                <Box sx={ {| mt = 5; display = "flex"; p = 20 |} }>
                    <CircularProgress />
                </Box>
                """

        let fixPrecision n d =
            (d |> float)
            |> Utils.Math.fixPrecision n
            |> string

        let onClickOk =
            fun () -> props.closeOrder ()

        let content =
            JSX.jsx
                $"""
            import CardHeader from '@mui/material/CardHeader';
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';
            import Paper from '@mui/material/Paper';

            <div>

            <CardHeader
                title={state.Order |> showOrderName}
                titleTypographyProps={ {| variant = "h6" |} }
            ></CardHeader>
            <CardContent>
                <Stack direction={"column"} spacing={3} >
                    {
                        match state.Order with
                        | Some ord ->
                            if ord.Orderable.Components |> Array.isEmpty then JSX.jsx $"<></>"
                            else
                                ord.Orderable.Components
                                |> Array.map _.Name
                                |> Array.map (fun s -> s, s)
                                |> select false "Componenten" state.SelectedComponent (ChangeComponent >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        match state.Order with
                        | Some ord ->
                            ord.Orderable.Components
                            |> Array.tryFind (fun c -> state.SelectedComponent.IsNone || c.Name = state.SelectedComponent.Value)
                            |> Option.bind _.Dose.Quantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false (Terms.``Order Quantity`` |> getTerm "Hoeveelheid") None ignore //(ChangeOrderableDoseQuantity >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        match state.Order with
                        | Some ord ->
                            if ord.Orderable.Components |> Array.isEmpty then JSX.jsx $"<></>"
                            else
                                itms
                                |> Array.map _.Name
                                |> Array.map (fun s -> s, s)
                                |> select false "Stoffen" state.SelectedItem (ChangeItem >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        match state.Order with
                        | Some ord ->
                            ord.Prescription.Frequency.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false (Terms.``Order Frequency`` |> getTerm "Frequentie") None (ChangeFrequency >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        match substIndx, state.Order with
                        | Some i, Some _ when itms |> Array.length > 0->
                            let label, vals =
                                itms[i].Dose.Quantity.Variable.Vals
                                |> Option.map (fun v ->
                                    (Terms.``Continuous Medication Dose``
                                    |> getTerm "Keer Dosis"
                                    |> fun s -> $"{s} ({v.Unit})"),
                                    v.Value
                                    |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                                    |> Array.distinctBy snd
                                )
                                |> Option.defaultValue ("", [||])

                            vals
                            |> select false label None (ChangeSubstanceDoseQuantity >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Prescription.IsContinuous |> not &&
                                                itms |> Array.length > 0 ->
                            let dispatch =
                                if useAdjust then ChangeSubstancePerTimeAdjust >> dispatch
                                else ChangeSubstancePerTime >> dispatch
                            let label, vals =
                                if useAdjust then
                                    itms[i].Dose.PerTimeAdjust.Variable.Vals
                                else
                                    itms[i].Dose.PerTime.Variable.Vals
                                |> Option.map (fun v ->
                                    (Terms.``Order Adjusted dose``
                                    |> getTerm "Dosering"
                                    |> fun s -> $"{s} ({v.Unit})"),
                                    v.Value
                                    |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                                    |> Array.distinctBy snd
                                )
                                |> Option.defaultValue ("", [||])

                            vals
                            |> select false label None dispatch
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Prescription.IsContinuous &&
                                                itms |> Array.length > 0 ->
                            let dispatch = if useAdjust then ChangeSubstanceRateAdjust >> dispatch else ChangeSubstanceRate >> dispatch

                            if useAdjust then
                                itms[i].Dose.RateAdjust.Variable.Vals
                            else
                                itms[i].Dose.Rate.Variable.Vals
                            |> Option.map (fun v ->
                                v.Value
                                |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                                |> Array.distinctBy snd
                            )
                            |> Option.defaultValue [||]
                            |> select false (Terms.``Order Adjusted dose`` |> getTerm "Dosering") None dispatch
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        match state.Order with
                        | Some ord ->
                            ord.Orderable.Dose.Quantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false (Terms.``Order Quantity`` |> getTerm "Hoeveelheid") None (ChangeOrderableDoseQuantity >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        match substIndx, state.Order with
                        | Some i, Some _ when itms |> Array.length > 0 ->
                            itms[i].ComponentConcentration.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false (Terms.``Order Concentration`` |> getTerm "Sterkte") None (ChangeSubstanceComponentConcentration >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        match state.Order with
                        | Some ord ->
                            ord.Orderable.Dose.Rate.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false (Terms.``Order Drip rate`` |> getTerm "Pompsnelheid") None (ChangeOrderableDoseRate >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        match state.Order with
                        | Some ord ->
                            ord.Prescription.Time.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 2} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false (Terms.``Order Administration time`` |> getTerm "Inloop tijd") None (ChangeTime >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                </Stack>

            </CardContent>
            <CardActions>
                    <Button onClick={onClickOk}>
                        {Terms.``Ok `` |> getTerm "Ok"}
                    </Button>
            </CardActions>
            </div>
            """

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Card from '@mui/material/Card';
        import CardActions from '@mui/material/CardActions';
        import CardContent from '@mui/material/CardContent';
        import Button from '@mui/material/Button';
        import Typography from '@mui/material/Typography';

        <Card variant="outlined">
                {content}
                {progress}
        </Card>
        """


