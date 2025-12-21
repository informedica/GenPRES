namespace Views



module Order =

    open Fable.Core
    open Fable.React
    open Feliz
    open Shared.Types
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
            | ChangeComponentDoseQuantity of string option
            | ChangeItem of string option
            | ChangeFrequency of string option
            | ChangeTime of string option
            | ChangeSubstanceDoseQuantity of string option
            | ChangeSubstanceDoseQuantityAdjust of string option
            | ChangeSubstancePerTime of string option
            | ChangeSubstancePerTimeAdjust of string option
            | ChangeSubstanceRate of string option
            | ChangeSubstanceRateAdjust of string option
            | ChangeSubstanceComponentConcentration of cmp: string * sbst: string * string option
            | ChangeSubstanceOrderableConcentration of string option
            | ChangeSubstanceOrderableQuantity of string option
            | ChangeOrderableDoseQuantity of string option
            | ChangeOrderableDoseRate of string option
            | ChangeOrderableQuantity of string option
            | UpdateOrderScenario of Order
            | ResetOrderScenario


        let init (ctx : Deferred<OrderContext>) =
            let ord, cmp, itm =
                match ctx with
                | Resolved ctx ->
                    match ctx.Scenarios with
                    | [| sc |] ->

                        let ord = sc.Order
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
            updateOrderScenario
            resetOrderScenario
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

            let setVar (s : string option) (var : Variable) =
                { var with
                    IsNonZeroPositive = s.IsNone
                    Vals =
                        if s.IsNone then None
                        else var.Vals |> setVu s
                }

            let setOvar s (ovar: OrderVariable) =
                { ovar with Variable = ovar.Variable |> setVar s }

            match msg with

            | UpdateOrderScenario ord ->

                OrderLoader.create state.SelectedComponent state.SelectedItem ord
                |> updateOrderScenario

                { state with
                    Order = None
                }
                , Cmd.none

            | ResetOrderScenario ->
                match state.Order with
                | Some ord ->
                    OrderLoader.create state.SelectedComponent state.SelectedItem ord
                    |> resetOrderScenario
                | None -> ()

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

            | ChangeComponentDoseQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.map (fun cmp ->
                                            match state.SelectedComponent with
                                            | Some c when cmp.Name = c ->
                                                { cmp with
                                                    Dose =
                                                        { cmp.Dose with
                                                            Quantity =
                                                                cmp.Dose.Quantity
                                                                |> setOvar s
                                                        }
                                                }
                                            | _ -> cmp
                                        )
                                }
                        }
                        |> UpdateOrderScenario

                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeItem itm ->
                match itm with
                | None -> state, Cmd.none
                | Some _ -> { state with SelectedItem = itm }, Cmd.none

            | ChangeFrequency s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Schedule =
                                { ord.Schedule with
                                    Frequency =
                                        ord.Schedule.Frequency
                                        |> setOvar s
                                }
                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeTime s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Schedule =
                                { ord.Schedule with
                                    Time =
                                        ord.Schedule.Time
                                        |> setOvar s
                                }
                        }
                        |> UpdateOrderScenario
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
                                                                                itm.Dose.Quantity
                                                                                |> setOvar s
                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }
                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceDoseQuantityAdjust s ->
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
                                                                            QuantityAdjust =
                                                                                itm.Dose.QuantityAdjust
                                                                                |> setOvar s
                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }
                        }
                        |> UpdateOrderScenario
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
                                                                                itm.Dose.PerTime
                                                                                |> setOvar s
                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
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
                                                                                itm.Dose.PerTimeAdjust
                                                                                |> setOvar s
                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
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
                                                                                itm.Dose.Rate
                                                                                |> setOvar s
                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
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
                                                                                itm.Dose.RateAdjust
                                                                                |> setOvar s
                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceComponentConcentration (cname, iname, s) ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 && cmp.Name <> cname then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    ComponentConcentration =
                                                                        itm.ComponentConcentration
                                                                        |> setOvar s
                                                                }
                                                            | _ -> 
                                                                if itm.Name <> iname then itm
                                                                else
                                                                    { itm with
                                                                        ComponentConcentration =
                                                                            itm.ComponentConcentration
                                                                            |> setOvar s
                                                                    }

                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceOrderableConcentration s ->
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
                                                                    OrderableConcentration =
                                                                        itm.OrderableConcentration
                                                                        |> setOvar s
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceOrderableQuantity s ->
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
                                                                    OrderableQuantity =
                                                                        itm.OrderableQuantity
                                                                        |> setOvar s
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
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
                                                ord.Orderable.Dose.Quantity
                                                |> setOvar s
                                        }
                                }

                        }
                        |> UpdateOrderScenario
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
                                                ord.Orderable.Dose.Rate
                                                |> setOvar s
                                        }
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    OrderableQuantity =
                                        ord.Orderable.OrderableQuantity
                                        |> setOvar s
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

        let showOrderName (ord : Order option) =
            match ord with
            | Some ord ->
                let form =
                    ord.Orderable.Components
                    |> Array.tryHead
                    |> Option.map _.Form
                    |> Option.defaultValue ""
                $"{ord.Orderable.Name} {form}"
            | None -> "order is loading ..."


    open Elmish


    [<JSX.Component>]
    let View (props:
        {|
            orderContext: Deferred<OrderContext>
            updateOrderScenario: OrderContext -> unit
            refreshOrderScenario : OrderContext -> unit
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
            match props.orderContext with
            | Resolved pr ->
                pr.Scenarios
                |> Array.tryExactlyOne
                |> Option.map (fun sc -> sc.UseAdjust)
                |> Option.defaultValue false
            | _ -> false

        let updateOrderScenario (ol : OrderLoader) =
            match props.orderContext with
            | Resolved ctx ->
                { ctx with
                    Scenarios =
                        ctx.Scenarios
                        |> Array.map (fun sc ->
                            if sc.Order.Id <> ol.Order.Id then sc
                            else
                                {
                                    sc with
                                        Component = ol.Component
                                        Item = ol.Item
                                        Order = ol.Order
                                }
                        )
                }
                |> props.updateOrderScenario
            | _ -> ()

        let resetOrderScenario (ol : OrderLoader) =
            match props.orderContext with
            | Resolved ctx ->
                { ctx with
                    Scenarios =
                        ctx.Scenarios
                        |> Array.map (fun sc ->
                            if sc.Order.Id <> ol.Order.Id then sc
                            else
                                {
                                    sc with
                                        Component = ol.Component
                                        Item = ol.Item
                                        Order = ol.Order
                                }
                        )
                }
                |> props.refreshOrderScenario
            | _ -> ()

        let state, dispatch =
            React.useElmish (
                init props.orderContext,
                update updateOrderScenario resetOrderScenario,
                [| box props.orderContext |]
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
            match props.orderContext with
            | Resolved _ -> JSX.jsx $"<></>"
            | _ ->
                JSX.jsx
                    $"""
                import CircularProgress from '@mui/material/CircularProgress';

                <Box sx={ {| mt = 5; display = "flex"; p = 20 |} }>
                    <CircularProgress />
                </Box>
                """

        let fixPrecision = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision


        let onClickOk =
            fun () -> props.closeOrder ()


        let onClickReset =
            fun () ->
                ResetOrderScenario |> dispatch

        let headerSx = {| backgroundColor = Mui.Colors.Blue.``50`` |}

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
                sx = {headerSx}
                title={state.Order |> showOrderName}
                titleTypographyProps={ {| variant = "h6" |} }
            ></CardHeader>
            <CardContent>
                <Stack direction={"column"} spacing={3} >
                    {
                        // component name
                        match state.Order with
                        | Some ord ->
                            if ord.Orderable.Components |> Array.length <= 1 then JSX.jsx $"<></>"
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
                        // component dose quantity
                        match state.Order with
                        | Some ord when ord.Orderable.Components |> Array.length > 1 &&
                                        itms |> Array.isEmpty ->
                            ord.Orderable.Components
                            |> Array.tryFind (fun c -> state.SelectedComponent.IsNone || c.Name = state.SelectedComponent.Value)
                            |> Option.bind _.Dose.Quantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false (Terms.``Order Quantity`` |> getTerm "Hoeveelheid") None (ChangeComponentDoseQuantity >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        // substance name
                        match state.Order with
                        | Some ord ->
                            if ord.Orderable.Components |> Array.isEmpty ||
                               itms |> Array.length <= 1 then JSX.jsx $"<></>"
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
                        // frequency
                        match state.Order with
                        | Some ord ->
                            ord.Schedule.Frequency.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false (Terms.``Order Frequency`` |> getTerm "Frequentie") None (ChangeFrequency >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        // substance dose quantity
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous |> not &&
                                                itms |> Array.length > 0 ->
                            let label, vals =
                                itms[i].Dose.Quantity.Variable.Vals
                                |> Option.map (fun v ->
                                    (Terms.``Order Dose``
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
                        // substance dose quantity adjust
                        match substIndx, state.Order with
                        | Some i, Some ord when (ord.Schedule.IsOnce || ord.Schedule.IsOnceTimed) &&
                                                itms |> Array.length > 0 && useAdjust ->
                            let label, vals =
                                itms[i].Dose.QuantityAdjust.Variable.Vals
                                |> Option.map (fun v ->
                                    (Terms.``Order Adjusted dose``
                                    |> getTerm "Keer Dosis"
                                    |> fun s -> $"{s} ({v.Unit})"),
                                    v.Value
                                    |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                                    |> Array.distinctBy snd
                                )
                                |> Option.defaultValue ("", [||])

                            vals
                            |> select false label None (ChangeSubstanceDoseQuantityAdjust >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        // substance dose per time
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous |> not &&
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
                        // substance dose rate
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous &&
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
                        // orderable dose quantity
                        match state.Order with
                        | Some ord ->
                            ord.Orderable.Dose.Quantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false "Toedien Hoeveelheid" None (ChangeOrderableDoseQuantity >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        // substance component concentration
                        match substIndx, state.Order with
                        | Some i, Some ord ->
                            match itms |> Array.tryItem i with
                            | Some itm ->
                                let cname, iname = 
                                    ord.Orderable.Components |> Array.tryHead |> Option.map _.Name |> Option.defaultValue ""
                                    , itm.Name

                                let change = fun s -> (cname, iname, s) |> ChangeSubstanceComponentConcentration

                                itm.ComponentConcentration.Variable.Vals
                                |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}"))
                                |> Option.defaultValue [||]
                                |> fun xs -> if xs |> Array.length <= 1 then [||] else xs
                                |> select false "Product Sterkte" None (change >> dispatch)
                            | None ->
                                match 
                                    ord.Orderable.Components
                                    |> Array.tryFind (fun c -> state.SelectedComponent.IsNone || c.Name = state.SelectedComponent.Value) with
                                | Some cmp ->
                                    match cmp.Items |> Array.tryFind (fun i -> i.Name = cmp.Name) with
                                    | Some itm -> 
                                        let change = fun s -> (cmp.Name, itm.Name, s) |> ChangeSubstanceComponentConcentration

                                        itm.ComponentConcentration.Variable.Vals
                                        |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d} {v.Unit}"))
                                        |> Option.defaultValue [||]
                                        |> select false "Product Sterkte" None (change >> dispatch)
                                    | None -> 
                                        [||]
                                        |> select true "" None ignore
                                | None -> 
                                    [||]
                                    |> select true "" None ignore

                        | _ ->
                            [||]
                            |> select true "" None ignore

                    }
                    {
                        // substance orderable concentration
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous |> not &&
                                                itms |> Array.length > 0 &&
                                                ord.Orderable.Components |> Array.length > 1 ->
                            itms[i].OrderableConcentration.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false $"{itms[i].Name |> String.capitalize} Concentratie" None (ChangeSubstanceOrderableConcentration >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        // substance orderable quantity
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous &&
                                                itms |> Array.length > 0 &&
                                                ord.Orderable.Components |> Array.length > 1 ->
                            itms[i].OrderableQuantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false $"{itms[i].Name |> String.capitalize} Hoeveelheid" None (ChangeSubstanceOrderableQuantity >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                    {
                        // orderable quantity
                        match state.Order with
                        | Some ord ->
                            ord.Orderable.OrderableQuantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false "Bereiding Hoeveelheid" None (ChangeOrderableQuantity >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }

                    {
                        // orderable dose rate
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
                        // administration time
                        match state.Order with
                        | Some ord ->
                            ord.Schedule.Time.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 2} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> Array.distinctBy snd
                            |> select false (Terms.``Order Administration time`` |> getTerm "Inloop tijd") None (ChangeTime >> dispatch)
                        | _ ->
                            [||]
                            |> select true "" None ignore
                    }
                </Stack>
                {progress}
            </CardContent>
            <CardActions >
                    <Button onClick={onClickOk}>
                        {Terms.``Ok `` |> getTerm "Ok"}
                    </Button>
                    <Button onClick={onClickReset} startIcon={Mui.Icons.RefreshIcon}>
                        Reset
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

        <Card variant="outlined" raised={true}>
                {content}
        </Card>
        """


