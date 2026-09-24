namespace Views


module Order =

    open Fable.Core
    open Fable.React
    open Feliz
    open Shared.Types
    open Shared.Models.Order
    open Shared
    open OrderContextMachine
    open Elmish
    open FSharp.Core


    module private Elmish =


        /// What the dialog holds of its own: the component and the item picked, never the
        /// order, which is the one the context shown has.
        type State =
            {
                SelectedComponent: string option
                SelectedItem: string option
            }

        type Msg =
            | ChangeComponent of string option
            | ChangeComponentOrderableQuantity of string option
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
            // Frequency property commands
            | DecreaseFrequencyProperty
            | IncreaseFrequencyProperty
            | SetMinFrequencyProperty
            | SetMaxFrequencyProperty
            | SetMedianFrequencyProperty
            // Dose Quantity property commands
            | DecreaseDoseQuantityProperty of ntimes: int * useCalc: bool
            | IncreaseDoseQuantityProperty of ntimes: int * useCalc: bool
            | SetMinDoseQuantityProperty
            | SetMaxDoseQuantityProperty
            | SetMedianDoseQuantityProperty
            // Rate property commands
            | DecreaseDoseRateProperty of ntimes: int * useCalc: bool
            | IncreaseDoseRateProperty of ntimes: int * useCalc: bool
            | SetMinDoseRateProperty
            | SetMaxDoseRateProperty
            | SetMedianDoseRateProperty
            // Component Quantity property commands
            | DecreaseComponentQuantityProperty of ntimes: int * useCalc: bool
            | IncreaseComponentQuantityProperty of ntimes: int * useCalc: bool
            | SetMinComponentQuantityProperty
            | SetMaxComponentQuantityProperty
            | SetMedianComponentQuantityProperty


        /// The component and the item picked, seeded from the one scenario of the context
        /// shown: the scenario's own, or the first component and its first substance.
        let init (ctx: OrderContextView) =
            let cmp, itm =
                match ctx with
                | OrderContextView.Settled ctx
                | OrderContextView.Changing ctx ->
                    match ctx.Scenarios with
                    | [| sc |] ->

                        let ord = sc.Order
                        let cmp = sc.Component
                        let itm = sc.Item

                        match ord.Orderable.Components with
                        | [||] -> None, None
                        | _ ->
                            ord.Orderable.Components
                            |> Array.tryFind (fun c -> cmp.IsNone || c.Name = cmp.Value)
                            |> Option.map (fun c ->
                                // only use substances that are not additional
                                let substs = c.Items |> Array.filter (_.IsAdditional >> not)

                                if substs |> Array.isEmpty then
                                    Some c.Name, None
                                else
                                    let s =
                                        substs
                                        |> Array.tryFind (fun i -> i.Name |> Some = itm)
                                        |> Option.map _.Name
                                        |> Option.defaultValue (substs[0].Name)
                                        |> Some

                                    Some c.Name, s
                            )
                            |> Option.defaultValue (None, None)

                    | _ -> None, None

                | _ -> None, None

            {
                SelectedComponent = cmp
                SelectedItem = itm
            },
            Cmd.none


        let update
            updateOrderScenario
            resetOrderScenario
            (stepper:
                {|
                    setFreqMin: OrderLoader -> unit
                    setFreqDec: OrderLoader -> unit
                    setFreqMed: OrderLoader -> unit
                    setFreqInc: OrderLoader -> unit
                    setFreqMax: OrderLoader -> unit

                    setRateMin: OrderLoader -> unit
                    setRateDec: int * bool -> OrderLoader -> unit
                    setRateMed: OrderLoader -> unit
                    setRateInc: int * bool -> OrderLoader -> unit
                    setRateMax: OrderLoader -> unit

                    setDoseQtyMin: OrderLoader -> unit
                    setDoseQtyDec: int * bool -> OrderLoader -> unit
                    setDoseQtyMed: OrderLoader -> unit
                    setDoseQtyInc: int * bool -> OrderLoader -> unit
                    setDoseQtyMax: OrderLoader -> unit

                    setComponentQtyMin: OrderLoader -> unit
                    setComponentQtyDec: int * bool -> OrderLoader -> unit
                    setComponentQtyMed: OrderLoader -> unit
                    setComponentQtyInc: int * bool -> OrderLoader -> unit
                    setComponentQtyMax: OrderLoader -> unit

                |})
            (shown: Order option)
            (msg: Msg)
            (state: State)
            : State * Cmd<Msg>
            =
            let setOvar = OrderVariable.setOvar

            // every change and every step is over the order shown, the one the context sent
            // has while a change is under way; the lane holds it, the dialog holds none
            let handleNav nav =
                match shown with
                | None -> state, Cmd.none
                | Some ord ->
                    OrderLoader.create state.SelectedComponent state.SelectedItem ord |> nav
                    state, Cmd.none

            // a change to the order shown, sent to the lane; nothing to change without one
            let over (f: Order -> Order) =
                match shown with
                | Some ord -> state, Cmd.ofMsg (UpdateOrderScenario(f ord))
                | None -> state, Cmd.none

            // a change to the component picked, by name
            let overComponent (f: Shared.Types.Component -> Shared.Types.Component) =
                over (fun ord ->
                    { ord with
                        Order.Orderable.Components =
                            ord.Orderable.Components
                            |> Array.map (fun cmp ->
                                match state.SelectedComponent with
                                | Some c when cmp.Name = c -> f cmp
                                | _ -> cmp
                            )
                    }
                )

            // a change to the item picked, in the first component
            let overItem (f: Shared.Types.Item -> Shared.Types.Item) =
                over (fun ord ->
                    { ord with
                        Order.Orderable.Components =
                            ord.Orderable.Components
                            |> Array.mapi (fun i cmp ->
                                if i > 0 then
                                    cmp
                                else
                                    { cmp with
                                        Items =
                                            cmp.Items
                                            |> Array.map (fun itm ->
                                                match state.SelectedItem with
                                                | Some subst when subst = itm.Name -> f itm
                                                | _ -> itm
                                            )
                                    }
                            )
                    }
                )

            match msg with

            | UpdateOrderScenario ord ->

                OrderLoader.create state.SelectedComponent state.SelectedItem ord
                |> updateOrderScenario

                state, Cmd.none

            | ResetOrderScenario ->
                match shown with
                | Some ord ->
                    OrderLoader.create state.SelectedComponent state.SelectedItem ord
                    |> resetOrderScenario
                | None -> ()

                state, Cmd.none

            | ChangeComponent cmp ->
                match cmp with
                | None -> state, Cmd.none
                | Some _ ->
                    { state with
                        SelectedComponent = cmp
                        SelectedItem =
                            if state.SelectedComponent = cmp then
                                state.SelectedItem
                            else
                                None
                    },
                    Cmd.none

            | ChangeComponentOrderableQuantity s ->
                overComponent (fun cmp -> { cmp with OrderableQuantity = cmp.OrderableQuantity |> setOvar s })

            | ChangeItem itm ->
                match itm with
                | None -> state, Cmd.none
                | Some _ -> { state with SelectedItem = itm }, Cmd.none

            | ChangeFrequency s ->
                over (fun ord -> { ord with Order.Schedule.Frequency = ord.Schedule.Frequency |> setOvar s })
            | ChangeTime s -> over (fun ord -> { ord with Order.Schedule.Time = ord.Schedule.Time |> setOvar s })
            | ChangeSubstanceDoseQuantity s ->
                overItem (fun itm -> { itm with Item.Dose.Quantity = itm.Dose.Quantity |> setOvar s })
            | ChangeSubstanceDoseQuantityAdjust s ->
                overItem (fun itm -> { itm with Item.Dose.QuantityAdjust = itm.Dose.QuantityAdjust |> setOvar s })
            | ChangeSubstancePerTime s ->
                overItem (fun itm -> { itm with Item.Dose.PerTime = itm.Dose.PerTime |> setOvar s })
            | ChangeSubstancePerTimeAdjust s ->
                overItem (fun itm -> { itm with Item.Dose.PerTimeAdjust = itm.Dose.PerTimeAdjust |> setOvar s })
            | ChangeSubstanceRate s -> overItem (fun itm -> { itm with Item.Dose.Rate = itm.Dose.Rate |> setOvar s })
            | ChangeSubstanceRateAdjust s ->
                overItem (fun itm -> { itm with Item.Dose.RateAdjust = itm.Dose.RateAdjust |> setOvar s })

            // the item picked and the item named, in the first component and the one named
            | ChangeSubstanceComponentConcentration(cname, iname, s) ->
                let set (itm: Shared.Types.Item) =
                    { itm with ComponentConcentration = itm.ComponentConcentration |> setOvar s }

                over (fun ord ->
                    { ord with
                        Order.Orderable.Components =
                            ord.Orderable.Components
                            |> Array.mapi (fun i cmp ->
                                if i > 0 && cmp.Name <> cname then
                                    cmp
                                else
                                    { cmp with
                                        Items =
                                            cmp.Items
                                            |> Array.map (fun itm ->
                                                match state.SelectedItem with
                                                | Some subst when subst = itm.Name -> set itm
                                                | _ -> if itm.Name <> iname then itm else set itm
                                            )
                                    }
                            )
                    }
                )

            | ChangeSubstanceOrderableConcentration s ->
                overItem (fun itm -> { itm with OrderableConcentration = itm.OrderableConcentration |> setOvar s })
            | ChangeSubstanceOrderableQuantity s ->
                overItem (fun itm -> { itm with OrderableQuantity = itm.OrderableQuantity |> setOvar s })
            | ChangeOrderableDoseQuantity s ->
                over (fun ord -> { ord with Order.Orderable.Dose.Quantity = ord.Orderable.Dose.Quantity |> setOvar s })
            | ChangeOrderableDoseRate s ->
                over (fun ord -> { ord with Order.Orderable.Dose.Rate = ord.Orderable.Dose.Rate |> setOvar s })
            | ChangeOrderableQuantity s ->
                over (fun ord ->
                    { ord with Order.Orderable.OrderableQuantity = ord.Orderable.OrderableQuantity |> setOvar s }
                )

            // == Frequency ==
            | SetMinFrequencyProperty -> handleNav stepper.setFreqMin
            | DecreaseFrequencyProperty -> handleNav stepper.setFreqDec
            | SetMedianFrequencyProperty -> handleNav stepper.setFreqMed
            | IncreaseFrequencyProperty -> handleNav stepper.setFreqInc
            | SetMaxFrequencyProperty -> handleNav stepper.setFreqMax
            // == Rate ==
            | SetMinDoseRateProperty -> handleNav stepper.setRateMin
            | DecreaseDoseRateProperty(n, uc) -> handleNav (stepper.setRateDec (n, uc))
            | SetMedianDoseRateProperty -> handleNav stepper.setRateMed
            | IncreaseDoseRateProperty(n, uc) -> handleNav (stepper.setRateInc (n, uc))
            | SetMaxDoseRateProperty -> handleNav stepper.setRateMax
            // == DoseQty ==
            | SetMinDoseQuantityProperty -> handleNav stepper.setDoseQtyMin
            | DecreaseDoseQuantityProperty(n, uc) -> handleNav (stepper.setDoseQtyDec (n, uc))
            | SetMedianDoseQuantityProperty -> handleNav stepper.setDoseQtyMed
            | IncreaseDoseQuantityProperty(n, uc) -> handleNav (stepper.setDoseQtyInc (n, uc))
            | SetMaxDoseQuantityProperty -> handleNav stepper.setDoseQtyMax
            // == ComponentQty ==
            | SetMinComponentQuantityProperty -> handleNav stepper.setComponentQtyMin
            | DecreaseComponentQuantityProperty(n, uc) -> handleNav (stepper.setComponentQtyDec (n, uc))
            | SetMedianComponentQuantityProperty -> handleNav stepper.setComponentQtyMed
            | IncreaseComponentQuantityProperty(n, uc) -> handleNav (stepper.setComponentQtyInc (n, uc))
            | SetMaxComponentQuantityProperty -> handleNav stepper.setComponentQtyMax


        let showOrderName (ord: Order option) =
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


    let private msgToField msg =
        match msg with
        | ChangeFrequency _
        | SetMinFrequencyProperty
        | DecreaseFrequencyProperty
        | SetMedianFrequencyProperty
        | IncreaseFrequencyProperty
        | SetMaxFrequencyProperty -> Some "frequency"
        | ChangeTime _ -> Some "time"
        | ChangeSubstanceDoseQuantity _ -> Some "substDoseQty"
        | ChangeSubstanceDoseQuantityAdjust _ -> Some "substDoseQtyAdj"
        | ChangeSubstancePerTime _
        | ChangeSubstancePerTimeAdjust _ -> Some "substPerTime"
        | ChangeSubstanceRate _
        | ChangeSubstanceRateAdjust _ -> Some "substRate"
        | ChangeSubstanceComponentConcentration _ -> Some "substCompConc"
        | ChangeSubstanceOrderableConcentration _ -> Some "substOrdConc"
        | ChangeSubstanceOrderableQuantity _ -> Some "substOrdQty"
        | ChangeComponentOrderableQuantity _
        | SetMinComponentQuantityProperty
        | DecreaseComponentQuantityProperty _
        | SetMedianComponentQuantityProperty
        | IncreaseComponentQuantityProperty _
        | SetMaxComponentQuantityProperty -> Some "compOrdQty"
        | ChangeOrderableQuantity _ -> Some "ordQty"
        | ChangeOrderableDoseQuantity _
        | SetMinDoseQuantityProperty
        | DecreaseDoseQuantityProperty _
        | SetMedianDoseQuantityProperty
        | IncreaseDoseQuantityProperty _
        | SetMaxDoseQuantityProperty -> Some "ordDoseQty"
        | ChangeOrderableDoseRate _
        | SetMinDoseRateProperty
        | DecreaseDoseRateProperty _
        | SetMedianDoseRateProperty
        | IncreaseDoseRateProperty _
        | SetMaxDoseRateProperty -> Some "ordDoseRate"
        | _ -> None


    [<JSX.Component>]
    let View
        (props:
            {|
                orderContext: OrderContextView
                updateOrderScenario: OrderContext -> unit
                stepOrderScenario:
                    {|
                        // Frequency
                        setMinFrequency: OrderContext -> unit
                        decrFrequency: OrderContext -> unit
                        setMedianFrequency: OrderContext -> unit
                        incrFrequency: OrderContext -> unit
                        setMaxFrequency: OrderContext -> unit
                        // Rate
                        setMinRate: OrderContext -> unit
                        decrRate: OrderContext * int * bool -> unit
                        setMedianRate: OrderContext -> unit
                        incrRate: OrderContext * int * bool -> unit
                        setMaxRate: OrderContext -> unit
                        // Dose Quantity
                        setMinDoseQty: OrderContext -> unit
                        decrDoseQty: OrderContext * int * bool -> unit
                        setMedianDoseQty: OrderContext -> unit
                        incrDoseQty: OrderContext * int * bool -> unit
                        setMaxDoseQty: OrderContext -> unit
                        // Component Quantity
                        setMinComponentQty: OrderContext * string -> unit
                        decrComponentQty: OrderContext * string * int * bool -> unit
                        setMedianComponentQty: OrderContext * string -> unit
                        incrComponentQty: OrderContext * string * int * bool -> unit
                        setMaxComponentQty: OrderContext * string -> unit
                    |}
                refreshOrderScenario: OrderContext -> unit
                closeOrder: unit -> unit
                localizationTerms: Deferred<string[][]>
            |})
        =
        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let getTerm = Global.getLocalizedTerm props.localizationTerms lang

        // the context shown: settled, or the one sent while a change is under way
        let shownContext =
            match props.orderContext with
            | OrderContextView.Settled ctx
            | OrderContextView.Changing ctx -> Some ctx
            | OrderContextView.NoPatient -> None

        let shownOrder =
            shownContext
            |> Option.bind (fun ctx -> ctx.Scenarios |> Array.tryExactlyOne |> Option.map _.Order)

        let useAdjust =
            shownContext
            |> Option.bind (fun pr -> pr.Scenarios |> Array.tryExactlyOne |> Option.map _.UseAdjust)
            |> Option.defaultValue false

        let updateOrderScenario (ol: OrderLoader) =
            match props.orderContext with
            | OrderContextView.Settled ctx
            | OrderContextView.Changing ctx ->
                { ctx with
                    Scenarios =
                        ctx.Scenarios
                        |> Array.map (fun sc ->
                            if sc.Order.Id <> ol.Order.Id then
                                sc
                            else
                                { sc with
                                    Component = ol.Component
                                    Item = ol.Item
                                    Order = ol.Order
                                }
                        )
                }
                |> props.updateOrderScenario
            | _ -> ()

        let resetOrderScenario (ol: OrderLoader) =
            match props.orderContext with
            | OrderContextView.Settled ctx
            | OrderContextView.Changing ctx ->
                { ctx with
                    Scenarios =
                        ctx.Scenarios
                        |> Array.map (fun sc ->
                            if sc.Order.Id <> ol.Order.Id then
                                sc
                            else
                                { sc with
                                    Component = ol.Component
                                    Item = ol.Item
                                    Order = ol.Order
                                }
                        )
                }
                |> props.refreshOrderScenario
            | _ -> ()

        let stepper =
            let create nav =
                fun (ol: OrderLoader) ->
                    match props.orderContext with
                    | OrderContextView.Settled ctx
                    | OrderContextView.Changing ctx ->
                        { ctx with
                            Scenarios =
                                ctx.Scenarios
                                |> Array.map (fun sc ->
                                    if sc.Order.Id <> ol.Order.Id then
                                        sc
                                    else
                                        { sc with
                                            Component = ol.Component
                                            Item = ol.Item
                                            Order = ol.Order
                                        }
                                )
                        }
                        |> nav
                    | _ -> ()

            let createWithCmp nav =
                fun (ol: OrderLoader) ->
                    match props.orderContext with
                    | OrderContextView.Settled ctx
                    | OrderContextView.Changing ctx ->
                        match ol.Component with
                        | None -> ()
                        | Some cmp ->
                            let ctx =
                                { ctx with
                                    Scenarios =
                                        ctx.Scenarios
                                        |> Array.map (fun sc ->
                                            if sc.Order.Id <> ol.Order.Id then
                                                sc
                                            else
                                                { sc with
                                                    Component = ol.Component
                                                    Item = ol.Item
                                                    Order = ol.Order
                                                }
                                        )
                                }

                            nav (ctx, cmp)
                    | _ -> ()

            let createWithN nav =
                fun (n, uc) (ol: OrderLoader) ->
                    match props.orderContext with
                    | OrderContextView.Settled ctx
                    | OrderContextView.Changing ctx ->
                        match ol.Component with
                        | None -> ()
                        | Some _ ->
                            let ctx =
                                { ctx with
                                    Scenarios =
                                        ctx.Scenarios
                                        |> Array.map (fun sc ->
                                            if sc.Order.Id <> ol.Order.Id then
                                                sc
                                            else
                                                { sc with
                                                    Component = ol.Component
                                                    Item = ol.Item
                                                    Order = ol.Order
                                                }
                                        )
                                }

                            nav (ctx, n, uc)
                    | _ -> ()

            let createWithCmpN nav =
                fun (n, uc) (ol: OrderLoader) ->
                    match props.orderContext with
                    | OrderContextView.Settled ctx
                    | OrderContextView.Changing ctx ->
                        match ol.Component with
                        | None -> ()
                        | Some cmp ->
                            let ctx =
                                { ctx with
                                    Scenarios =
                                        ctx.Scenarios
                                        |> Array.map (fun sc ->
                                            if sc.Order.Id <> ol.Order.Id then
                                                sc
                                            else
                                                { sc with
                                                    Component = ol.Component
                                                    Item = ol.Item
                                                    Order = ol.Order
                                                }
                                        )
                                }

                            nav (ctx, cmp, n, uc)
                    | _ -> ()

            {|
                // Frequency
                setFreqMin = create props.stepOrderScenario.setMinFrequency
                setFreqDec = create props.stepOrderScenario.decrFrequency
                setFreqMed = create props.stepOrderScenario.setMedianFrequency
                setFreqInc = create props.stepOrderScenario.incrFrequency
                setFreqMax = create props.stepOrderScenario.setMaxFrequency
                // Dose Rate
                setRateMin = create props.stepOrderScenario.setMinRate
                setRateDec = createWithN props.stepOrderScenario.decrRate
                setRateMed = create props.stepOrderScenario.setMedianRate
                setRateInc = createWithN props.stepOrderScenario.incrRate
                setRateMax = create props.stepOrderScenario.setMaxRate
                // Dose Quantity
                setDoseQtyMin = create props.stepOrderScenario.setMinDoseQty
                setDoseQtyDec = createWithN props.stepOrderScenario.decrDoseQty
                setDoseQtyMed = create props.stepOrderScenario.setMedianDoseQty
                setDoseQtyInc = createWithN props.stepOrderScenario.incrDoseQty
                setDoseQtyMax = create props.stepOrderScenario.setMaxDoseQty
                // Component Quantity
                setComponentQtyMin = createWithCmp props.stepOrderScenario.setMinComponentQty
                setComponentQtyDec = createWithCmpN props.stepOrderScenario.decrComponentQty
                setComponentQtyMed = createWithCmp props.stepOrderScenario.setMedianComponentQty
                setComponentQtyInc = createWithCmpN props.stepOrderScenario.incrComponentQty
                setComponentQtyMax = createWithCmp props.stepOrderScenario.setMaxComponentQty
            |}

        let state, dispatch =
            React.useElmish (
                init props.orderContext,
                update updateOrderScenario resetOrderScenario stepper shownOrder,
                [| box props.orderContext |]
            )

        // the field whose change went out, and whether it shows that it is loading
        let changing, setChanging = React.useState<(string * bool) option> None

        // Clear the field changing when parent finishes recalculating
        React.useEffect (
            (fun () ->
                match props.orderContext with
                | OrderContextView.Settled _ -> setChanging None
                | _ -> ()
            ),
            [| box props.orderContext |]
        )

        let isOrderLoading =
            match props.orderContext with
            | OrderContextView.Changing _ -> true
            | OrderContextView.NoPatient
            | OrderContextView.Settled _ -> false

        let isFieldLoading field = isOrderLoading && changing = Some(field, true)

        // while a change is under way only the field changing may change again, and only
        // on an order solved through: the lane keeps one change pending, so a second field
        // would replace the first
        let solved = shownOrder |> Option.map isSolved |> Option.defaultValue false

        let rests field =
            isOrderLoading && (not solved || (changing |> Option.map fst) <> Some field)

        // Monotonic counter bumped on every new server response (a fresh Settled
        // orderContext). Passed into stepped selects so they reset their optimistic
        // step value even when the server returns the SAME value as before (e.g. a no-op
        // step when already at the maximum) — in that case the displayed value never changes, so the
        // value-based reset alone would leave the stale optimistic value on screen.
        let revisionRef = React.useRef 0
        let prevCtxRef = React.useRef props.orderContext

        if not (obj.ReferenceEquals(prevCtxRef.current, props.orderContext)) then
            prevCtxRef.current <- props.orderContext

            match props.orderContext with
            | OrderContextView.Settled _ -> revisionRef.current <- revisionRef.current + 1
            | _ -> ()

        let revision = revisionRef.current

        // Decrease/increase steps — inner (useCalc = false) and outer/first-last
        // (useCalc = true) — are reflected immediately via an optimistic value, so the
        // field must NOT show a loading indicator that would suggest the value hasn't
        // changed yet. (Navigable first/last use SetMin/SetMax messages, not these, so
        // they still show loading.)
        let isOptimisticStep msg =
            match msg with
            | DecreaseDoseRateProperty _
            | IncreaseDoseRateProperty _
            | DecreaseDoseQuantityProperty _
            | IncreaseDoseQuantityProperty _
            | DecreaseComponentQuantityProperty _
            | IncreaseComponentQuantityProperty _ -> true
            | _ -> false

        // Shadow dispatch to auto-track which field is changing, and whether it shows it
        let dispatch =
            let originalDispatch = dispatch

            fun msg ->
                msgToField msg
                |> Option.iter (fun f -> setChanging (Some(f, not (isOptimisticStep msg))))

                originalDispatch msg

        // the order shown, the one sent while a change is under way, so that the dialog stays
        // populated while the server is processing
        let displayOrder = shownOrder

        let itms =
            match displayOrder with
            | Some ord ->
                ord.Orderable.Components
                // only use the main component for dosing
                |> Array.tryFind (fun cmp -> state.SelectedComponent.IsNone || state.SelectedComponent.Value = cmp.Name)
                |> Option.map (fun cmp ->
                    // filter out additional items, they are not used for dosing
                    cmp.Items |> Array.filter (_.IsAdditional >> not)
                )
                |> Option.defaultValue [||]
            | _ -> [||]

        let substIndx =
            itms
            |> Array.tryFindIndex (fun i -> state.SelectedItem |> Option.map ((=) i.Name) |> Option.defaultValue false)
            |> function
                | None -> Some 0
                | Some i -> Some i

        let showDosingDivider =
            match displayOrder with
            | None -> false
            | Some ord ->
                let hasSubstIndx = substIndx.IsSome && itms |> Array.length > 0

                // substance dose quantity: not continuous, substIndx, itms > 0, has vals
                let hasSubstDoseQty =
                    hasSubstIndx
                    && ord.Schedule.IsContinuous |> not
                    && substIndx
                       |> Option.bind (fun i -> itms |> Array.tryItem i)
                       |> Option.bind (fun itm ->
                           itm.Dose.Quantity.Variable.Vals
                           |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       )
                       |> Option.defaultValue false

                // substance dose quantity adjust: once/onceTimed, useAdjust, substIndx, itms > 0, has vals
                let hasSubstDoseQtyAdj =
                    hasSubstIndx
                    && useAdjust
                    && (ord.Schedule.IsOnce || ord.Schedule.IsOnceTimed)
                    && substIndx
                       |> Option.bind (fun i -> itms |> Array.tryItem i)
                       |> Option.bind (fun itm ->
                           itm.Dose.QuantityAdjust.Variable.Vals
                           |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       )
                       |> Option.defaultValue false

                // substance dose per time: not continuous, substIndx, itms > 0, has vals
                let hasSubstPerTime =
                    hasSubstIndx
                    && ord.Schedule.IsContinuous |> not
                    && substIndx
                       |> Option.bind (fun i -> itms |> Array.tryItem i)
                       |> Option.bind (fun itm ->
                           let vals =
                               if useAdjust then
                                   itm.Dose.PerTimeAdjust.Variable.Vals
                               else
                                   itm.Dose.PerTime.Variable.Vals

                           vals |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       )
                       |> Option.defaultValue false

                // substance dose rate: continuous, substIndx, itms > 0, has vals
                let hasSubstRate =
                    hasSubstIndx
                    && ord.Schedule.IsContinuous
                    && substIndx
                       |> Option.bind (fun i -> itms |> Array.tryItem i)
                       |> Option.bind (fun itm ->
                           let vals =
                               if useAdjust then
                                   itm.Dose.RateAdjust.Variable.Vals
                               else
                                   itm.Dose.Rate.Variable.Vals

                           vals |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       )
                       |> Option.defaultValue false

                hasSubstDoseQty || hasSubstDoseQtyAdj || hasSubstPerTime || hasSubstRate

        let showPrepDivider =
            match displayOrder with
            | None -> false
            | Some ord ->
                let multiComponent = ord.Orderable.Components |> Array.length > 1

                // component orderable quantity: requires components > 1
                let hasCompOrdQty =
                    multiComponent
                    && ord.Orderable.Components
                       |> Array.tryFind (fun c ->
                           state.SelectedComponent.IsNone || c.Name = state.SelectedComponent.Value
                       )
                       |> Option.bind _.OrderableQuantity.Variable.Vals
                       |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       |> Option.defaultValue false

                // substance component concentration: requires substIndx and vals > 1
                let hasSubstCompConc =
                    substIndx
                    |> Option.bind (fun i -> itms |> Array.tryItem i)
                    |> Option.bind (fun itm ->
                        itm.ComponentConcentration.Variable.Vals
                        |> Option.map (fun v -> v.Value |> Array.length > 1)
                    )
                    |> Option.defaultValue false

                // substance orderable quantity: requires substIndx, itms > 0, components > 1, continuous
                let hasSubstOrbQty =
                    multiComponent
                    && ord.Schedule.IsContinuous
                    && substIndx
                       |> Option.bind (fun i -> itms |> Array.tryItem i)
                       |> Option.bind (fun itm ->
                           itm.OrderableQuantity.Variable.Vals
                           |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       )
                       |> Option.defaultValue false

                // substance orderable concentration: requires substIndx, itms > 0, components > 1, not continuous
                let hasSubstOrbConc =
                    multiComponent
                    && ord.Schedule.IsContinuous |> not
                    && substIndx
                       |> Option.bind (fun i -> itms |> Array.tryItem i)
                       |> Option.bind (fun itm ->
                           itm.OrderableConcentration.Variable.Vals
                           |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       )
                       |> Option.defaultValue false

                // orderable quantity: requires components > 1
                let hasOrbQty =
                    multiComponent
                    && ord.Orderable.OrderableQuantity.Variable.Vals
                       |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       |> Option.defaultValue false

                hasCompOrdQty
                || hasSubstCompConc
                || hasSubstOrbConc
                || hasSubstOrbQty
                || hasOrbQty

        let showAdminDivider =
            match displayOrder with
            | None -> false
            | Some ord ->
                // frequency: shown when has vals or single val (nav)
                let hasFrequency =
                    ord.Schedule.Frequency.Variable.Vals
                    |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                    |> Option.defaultValue false
                // orderable dose quantity: shown when not continuous and has vals
                let hasDoseQty =
                    ord.Schedule.IsContinuous |> not
                    && ord.Orderable.Dose.Quantity.Variable.Vals
                       |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       |> Option.defaultValue false
                // orderable dose rate: shown when continuous/timed/onceTimed
                // (stepper is always Some, so select renders even with empty vals)
                let hasDoseRate = ord.Schedule.IsContinuous || ord.Schedule.IsTimed || ord.Schedule.IsOnceTimed
                // administration time: shown when has vals
                let hasTime =
                    ord.Schedule.Time.Variable.Vals
                    |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                    |> Option.defaultValue false

                hasFrequency || hasDoseQty || hasDoseRate || hasTime

        let severityOf = ViewHelpers.severityOf

        // the component and the item selects are the dialog's own, never a request
        let select = ViewHelpers.orderSelect false false

        // a field's select: rests while another field is changing, shows it while its own is
        let selectFor field =
            ViewHelpers.orderSelect false (rests field) (isFieldLoading field)

        let loadingIndicator = ViewHelpers.inlineProgress isOrderLoading

        let fixPrecision = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision

        let onClickOk = fun () -> props.closeOrder ()

        let onClickReset = fun () -> ResetOrderScenario |> dispatch

        let headerSx =
            if isMobile then
                {|
                    backgroundColor = Mui.Styles.headerBgColor
                    paddingY = 0.5
                |}
            else
                {|
                    backgroundColor = Mui.Styles.headerBgColor
                    paddingY = 1
                |}

        let preparationDivider =
            if showPrepDivider then
                JSX.jsx $"""<Divider><Typography variant="caption">bereiding</Typography></Divider>"""
            else
                null

        let dosingDivider =
            if showDosingDivider then
                JSX.jsx $"""<Divider><Typography variant="caption">dosering</Typography></Divider>"""
            else
                null

        let administrationDivider =
            if showAdminDivider then
                JSX.jsx $"""<Divider><Typography variant="caption">toediening</Typography></Divider>"""
            else
                null

        let content =
            let createStepper = ViewHelpers.createStepper dispatch revision

            let contentSx =
                {|
                    paddingX = (if isMobile then 1.5 else 2)
                    paddingY = (if isMobile then 1 else 2)
                |}

            let componentSelect =
                match displayOrder with
                | Some ord ->
                    if ord.Orderable.Components |> Array.length <= 1 then
                        null
                    else
                        ord.Orderable.Components
                        |> Array.map _.Name
                        |> Array.map (fun s -> s, s)
                        |> select
                            false
                            "componenten"
                            state.SelectedComponent
                            (ChangeComponent >> dispatch)
                            None
                            false
                            Severity.Normal
                            None
                | _ -> null

            let itemSelect =
                match displayOrder with
                | Some ord ->
                    if ord.Orderable.Components |> Array.isEmpty || itms |> Array.length <= 1 then
                        null
                    else
                        itms
                        |> Array.map _.Name
                        |> Array.map (fun s -> s, s)
                        |> select
                            false
                            "stoffen"
                            state.SelectedItem
                            (ChangeItem >> dispatch)
                            None
                            false
                            Severity.Normal
                            None
                | _ -> null

            let substDoseQtySelect =
                match substIndx, displayOrder with
                | Some i, Some ord when ord.Schedule.IsContinuous |> not && itms |> Array.length > 0 ->
                    let label, vals =
                        itms[i].Dose.Quantity.Variable.Vals
                        |> Option.map (fun v ->
                            (Terms.``Order Dose`` |> getTerm "keer dosis" |> (fun s -> $"{s} ({v.Unit})")),
                            v.Value
                            |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                            |> Array.distinctBy snd
                        )
                        |> Option.defaultValue ("", [||])

                    let severity = itms[i].Dose.Quantity.Level |> severityOf

                    vals
                    |> selectFor
                        "substDoseQty"
                        label
                        None
                        (ChangeSubstanceDoseQuantity >> dispatch)
                        None
                        false
                        severity
                        None
                | _ -> null

            let substDoseQtyAdjSelect =
                match substIndx, displayOrder with
                | Some i, Some ord when
                    (ord.Schedule.IsOnce || ord.Schedule.IsOnceTimed)
                    && itms |> Array.length > 0
                    && useAdjust
                    ->
                    let label, vals =
                        itms[i].Dose.QuantityAdjust.Variable.Vals
                        |> Option.map (fun v ->
                            (Terms.``Order Adjusted dose``
                             |> getTerm "keer dosis"
                             |> fun s -> $"{s} ({v.Unit})"),
                            v.Value
                            |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                            |> Array.distinctBy snd
                        )
                        |> Option.defaultValue ("", [||])

                    let severity = itms[i].Dose.QuantityAdjust.Level |> severityOf

                    vals
                    |> selectFor
                        "substDoseQtyAdj"
                        label
                        None
                        (ChangeSubstanceDoseQuantityAdjust >> dispatch)
                        None
                        true
                        severity
                        None
                | _ -> null

            let substPerTimeSelect =
                match substIndx, displayOrder with
                | Some i, Some ord when ord.Schedule.IsContinuous |> not && itms |> Array.length > 0 ->
                    let dispatch =
                        if useAdjust then
                            ChangeSubstancePerTimeAdjust >> dispatch
                        else
                            ChangeSubstancePerTime >> dispatch

                    let label, vals =
                        if useAdjust then
                            itms[i].Dose.PerTimeAdjust.Variable.Vals
                        else
                            itms[i].Dose.PerTime.Variable.Vals
                        |> Option.map (fun v ->
                            (Terms.``Order Adjusted dose``
                             |> getTerm "dosering"
                             |> fun s -> $"{s} ({v.Unit})"),
                            v.Value
                            |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                            |> Array.distinctBy snd
                        )
                        |> Option.defaultValue ("", [||])

                    let severity =
                        if useAdjust then
                            itms[i].Dose.PerTimeAdjust.Level |> severityOf
                        else
                            itms[i].Dose.PerTime.Level |> severityOf

                    vals |> selectFor "substPerTime" label None dispatch None true severity None
                | _ -> null

            let substRateSelect =
                let stepper = None

                match substIndx, displayOrder with
                | Some i, Some ord when ord.Schedule.IsContinuous && itms |> Array.length > 0 ->
                    let dispatch =
                        if useAdjust then
                            ChangeSubstanceRateAdjust >> dispatch
                        else
                            ChangeSubstanceRate >> dispatch

                    let severity =
                        if useAdjust then
                            itms[i].Dose.RateAdjust.Level |> severityOf
                        else
                            itms[i].Dose.Rate.Level |> severityOf

                    let ovar =
                        if useAdjust then
                            itms[i].Dose.RateAdjust
                        else
                            itms[i].Dose.Rate

                    ovar
                    |> ViewHelpers.ovarVals (fixPrecision 3)
                    |> Array.distinctBy snd
                    |> selectFor
                        "substRate"
                        (Terms.``Order Adjusted dose`` |> getTerm "dosering")
                        None
                        dispatch
                        stepper
                        true
                        severity
                        None
                | _ -> null

            let compOrdQtySelect =
                match displayOrder with
                | Some ord when ord.Orderable.Components |> Array.length > 1 ->
                    let cmp =
                        ord.Orderable.Components
                        |> Array.tryFind (fun c ->
                            state.SelectedComponent.IsNone || c.Name = state.SelectedComponent.Value
                        )

                    let vals =
                        cmp
                        |> Option.map (_.OrderableQuantity >> ViewHelpers.ovarValsWithRange string 3)
                        |> Option.defaultValue [||]

                    let stepper =
                        let c = vals |> Array.length

                        let show =
                            match cmp with
                            | None -> false
                            | Some cmp ->
                                cmp.OrderableQuantity.Variable.Min.IsSome
                                && cmp.OrderableQuantity.Variable.Incr.IsSome
                                && cmp.OrderableQuantity.Variable.Max.IsSome
                                || c >= 1

                        if not show then
                            None
                        else
                            let solved = ord |> isSolved

                            // can jump to the min, median, or max
                            let navigable =
                                cmp
                                |> Option.map (_.OrderableQuantity >> OrderVariable.isNavigable)
                                |> Option.defaultValue false

                            createStepper
                                navigable
                                solved
                                SetMinComponentQuantityProperty
                                DecreaseComponentQuantityProperty
                                SetMedianComponentQuantityProperty
                                IncreaseComponentQuantityProperty
                                SetMaxComponentQuantityProperty
                                (cmp |> Option.bind (_.OrderableQuantity >> ViewHelpers.ovarStep string))

                    let severity =
                        cmp
                        |> Option.map (_.OrderableQuantity.Level >> severityOf)
                        |> Option.defaultValue Severity.Normal

                    vals
                    |> selectFor
                        "compOrdQty"
                        "bereiding hoeveelheid"
                        None
                        (ChangeComponentOrderableQuantity >> dispatch)
                        stepper
                        false
                        severity
                        None
                | _ -> null

            let substCompConcSelect =
                match substIndx, displayOrder with
                | Some i, Some ord ->
                    match itms |> Array.tryItem i with
                    | Some itm ->
                        let cname, iname =
                            ord.Orderable.Components
                            |> Array.tryHead
                            |> Option.map _.Name
                            |> Option.defaultValue "",
                            itm.Name

                        let change = fun s -> (cname, iname, s) |> ChangeSubstanceComponentConcentration

                        if
                            itm.ComponentConcentration.DefinedConstraints.Vals
                            |> Option.map (fun vu -> vu.Value |> Array.length > 1)
                            |> Option.defaultValue false
                        then
                            itm.ComponentConcentration
                            |> ViewHelpers.ovarVals (fixPrecision 3)
                            |> selectFor
                                "substCompConc"
                                "product sterkte"
                                None
                                (change >> dispatch)
                                None
                                false
                                Severity.Normal
                                None
                        else
                            null
                    | None ->
                        match
                            ord.Orderable.Components
                            |> Array.tryFind (fun c ->
                                state.SelectedComponent.IsNone || c.Name = state.SelectedComponent.Value
                            )
                        with
                        | Some cmp ->
                            match cmp.Items |> Array.tryFind (fun i -> i.Name = cmp.Name) with
                            | Some itm ->
                                let change = fun s -> (cmp.Name, itm.Name, s) |> ChangeSubstanceComponentConcentration

                                if
                                    itm.ComponentConcentration.DefinedConstraints.Vals
                                    |> Option.map (fun vu -> vu.Value |> Array.length > 1)
                                    |> Option.defaultValue false
                                then
                                    itm.ComponentConcentration
                                    |> ViewHelpers.ovarVals string
                                    |> selectFor
                                        "substCompConc"
                                        "product sterkte"
                                        None
                                        (change >> dispatch)
                                        None
                                        false
                                        Severity.Normal
                                        None
                                else
                                    null

                            | None -> null
                        | None -> null
                | _ -> null

            let substOrdQtySelect =
                match substIndx, displayOrder with
                | Some i, Some ord when
                    ord.Schedule.IsContinuous
                    && itms |> Array.length > 0
                    && ord.Orderable.Components |> Array.length > 1
                    ->
                    let severity = itms[i].OrderableQuantity.Level |> severityOf

                    itms[i].OrderableQuantity
                    |> ViewHelpers.ovarVals (fixPrecision 3)
                    |> selectFor
                        "substOrdQty"
                        $"{itms[i].Name} hoeveelheid"
                        None
                        (ChangeSubstanceOrderableQuantity >> dispatch)
                        None
                        false
                        severity
                        None
                | _ -> null

            let substOrdConcSelect =
                match substIndx, displayOrder with
                | Some i, Some ord when
                    ord.Schedule.IsContinuous |> not
                    && itms |> Array.length > 0
                    && ord.Orderable.Components |> Array.length > 1
                    ->
                    let severity = itms[i].OrderableConcentration.Level |> severityOf

                    itms[i].OrderableConcentration
                    |> ViewHelpers.ovarVals (fixPrecision 3)
                    |> selectFor
                        "substOrdConc"
                        $"{itms[i].Name} concentratie"
                        None
                        (ChangeSubstanceOrderableConcentration >> dispatch)
                        None
                        false
                        severity
                        None
                | _ -> null

            let ordQtySelect =
                match displayOrder with
                | Some ord when ord.Orderable.Components |> Array.length > 1 ->
                    let severity = ord.Orderable.OrderableQuantity.Level |> severityOf

                    ord.Orderable.OrderableQuantity
                    |> ViewHelpers.ovarVals string
                    |> selectFor
                        "ordQty"
                        "totale hoeveelheid"
                        None
                        (ChangeOrderableQuantity >> dispatch)
                        None
                        false
                        severity
                        None
                | _ -> null

            let frequencySelect =
                match displayOrder with
                | Some ord when ord.Schedule.IsDiscontinuous || ord.Schedule.IsTimed ->
                    let xs = ord.Schedule.Frequency |> ViewHelpers.ovarVals string

                    let stepper =
                        if xs |> Array.length <> 1 then
                            None
                        else
                            let solved = ord |> isSolved
                            // frequency: no jump to the min, median, or max
                            let navigable = false

                            createStepper
                                navigable
                                solved
                                SetMinFrequencyProperty
                                (fun _ -> DecreaseFrequencyProperty)
                                SetMedianFrequencyProperty
                                (fun _ -> IncreaseFrequencyProperty)
                                SetMaxFrequencyProperty
                                None

                    let severity = ord.Schedule.Frequency.Level |> severityOf

                    selectFor
                        "frequency"
                        (Terms.``Order Frequency`` |> getTerm "frequentie")
                        None
                        (ChangeFrequency >> dispatch)
                        stepper
                        false
                        severity
                        None
                        xs
                | _ -> null

            let ordDoseQtySelect =
                match displayOrder with
                | Some ord when ord.Schedule.IsContinuous |> not ->
                    let stepper =
                        ViewHelpers.createDoseQtyStepper
                            dispatch
                            revision
                            ord
                            SetMinDoseQuantityProperty
                            DecreaseDoseQuantityProperty
                            SetMedianDoseQuantityProperty
                            IncreaseDoseQuantityProperty
                            SetMaxDoseQuantityProperty

                    let severity = ord.Orderable.Dose.Quantity.Level |> severityOf

                    ord.Orderable.Dose.Quantity
                    |> ViewHelpers.ovarValsWithRange string 3
                    |> selectFor
                        "ordDoseQty"
                        "toedien hoeveelheid"
                        None
                        (ChangeOrderableDoseQuantity >> dispatch)
                        stepper
                        false
                        severity
                        None
                | _ -> null

            let ordDoseRateSelect =
                match displayOrder with
                | Some ord when ord.Schedule.IsContinuous || ord.Schedule.IsTimed || ord.Schedule.IsOnceTimed ->
                    let solved = ord |> isSolved
                    // can jump to the min, median, or max
                    let navigable = ord.Orderable.Dose.Rate |> OrderVariable.isNavigable

                    let stepper =
                        createStepper
                            navigable
                            solved
                            SetMinDoseRateProperty
                            DecreaseDoseRateProperty
                            SetMedianDoseRateProperty
                            IncreaseDoseRateProperty
                            SetMaxDoseRateProperty
                            (ord.Orderable.Dose.Rate |> ViewHelpers.ovarStep string)

                    let severity = ord.Orderable.Dose.Rate.Level |> severityOf

                    ord.Orderable.Dose.Rate
                    |> ViewHelpers.ovarValsWithRange string 3
                    |> selectFor
                        "ordDoseRate"
                        (Terms.``Order Drip rate`` |> getTerm "inloop snelheid")
                        None
                        (ChangeOrderableDoseRate >> dispatch)
                        stepper
                        false
                        severity
                        None
                | _ -> null

            let timeSelect =
                match displayOrder with
                | Some ord ->
                    let severity = ord.Schedule.Time.Level |> severityOf

                    ord.Schedule.Time
                    |> ViewHelpers.ovarVals (fixPrecision 2)
                    |> Array.distinctBy snd
                    |> selectFor
                        "time"
                        (Terms.``Order Administration time`` |> getTerm "inloop tijd")
                        None
                        (ChangeTime >> dispatch)
                        None
                        true
                        severity
                        None
                | _ -> null

            let titleSlotProps = {| title = {| variant = "h6" |} |}

            JSX.jsx
                $"""
            import CardHeader from '@mui/material/CardHeader';
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';
            import Paper from '@mui/material/Paper';
            import Divider from '@mui/material/Divider';
            <div>
            <CardHeader
                sx = {headerSx}
                title={displayOrder |> showOrderName}
                slotProps={titleSlotProps}
            ></CardHeader>
            <CardContent sx={contentSx}>
                <Stack direction={"column"} spacing={if isMobile then 1.5 else 3} >
                    {componentSelect}
                    {itemSelect}
                    {dosingDivider}
                    {substDoseQtySelect}
                    {substDoseQtyAdjSelect}
                    {substPerTimeSelect}
                    {substRateSelect}
                    {preparationDivider}
                    {compOrdQtySelect}
                    {substCompConcSelect}
                    {substOrdQtySelect}
                    {substOrdConcSelect}
                    {ordQtySelect}
                    {administrationDivider}
                    {frequencySelect}
                    {ordDoseQtySelect}
                    {ordDoseRateSelect}
                    {timeSelect}
                </Stack>
                {loadingIndicator}
            </CardContent>
            <CardActions >
                    <Button onClick={onClickOk}>
                        {Terms.``Ok `` |> getTerm "Ok"}
                    </Button>
                    <Button onClick={onClickReset} disabled={isOrderLoading} startIcon={Mui.Icons.RefreshIcon}>
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
