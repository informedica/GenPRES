namespace Views


module Order =

    open Fable.Core
    open Fable.Core.JsInterop
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
                | OrderContextView.Refused(ctx, _)
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

            // a change to the item the dialog shows: the item picked in the component picked, or, with
            // none picked, as after a switch of component, the first of its substances and the first
            // component, as the fields show them
            let overItem (f: Shared.Types.Item -> Shared.Types.Item) =
                over (fun ord ->
                    let cmpName =
                        ord.Orderable.Components
                        |> Array.tryFind (fun c ->
                            state.SelectedComponent.IsNone || state.SelectedComponent = Some c.Name
                        )
                        |> Option.map _.Name

                    { ord with
                        Order.Orderable.Components =
                            ord.Orderable.Components
                            |> Array.map (fun cmp ->
                                if Some cmp.Name <> cmpName then
                                    cmp
                                else
                                    let itmName =
                                        state.SelectedItem
                                        |> Option.orElse (
                                            cmp.Items |> Array.tryFind (_.IsAdditional >> not) |> Option.map _.Name
                                        )

                                    { cmp with
                                        Items =
                                            cmp.Items
                                            |> Array.map (fun itm -> if Some itm.Name = itmName then f itm else itm)
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


    /// What a message does: change a value, step it, send or reset the order, or change the dialog alone.
    [<RequireQualifiedAccess>]
    type private Kind =
        | Local
        | Change
        | Step of string
        | Update
        | Reset


    let private kindOf msg =
        match msg with
        | ChangeComponent _
        | ChangeItem _ -> Kind.Local
        | UpdateOrderScenario _ -> Kind.Update
        | ResetOrderScenario -> Kind.Reset
        | SetMinFrequencyProperty
        | SetMinDoseRateProperty
        | SetMinDoseQuantityProperty
        | SetMinComponentQuantityProperty -> Kind.Step "min"
        | DecreaseFrequencyProperty
        | DecreaseDoseRateProperty _
        | DecreaseDoseQuantityProperty _
        | DecreaseComponentQuantityProperty _ -> Kind.Step "dec"
        | SetMedianFrequencyProperty
        | SetMedianDoseRateProperty
        | SetMedianDoseQuantityProperty
        | SetMedianComponentQuantityProperty -> Kind.Step "med"
        | IncreaseFrequencyProperty
        | IncreaseDoseRateProperty _
        | IncreaseDoseQuantityProperty _
        | IncreaseComponentQuantityProperty _ -> Kind.Step "inc"
        | SetMaxFrequencyProperty
        | SetMaxDoseRateProperty
        | SetMaxDoseQuantityProperty
        | SetMaxComponentQuantityProperty -> Kind.Step "max"
        | _ -> Kind.Change


    /// The message for the trail: its case, what it carries, and its field.
    let private describeMsg msg =
        let value (s: string option) = s |> Option.defaultValue "none"

        let steps (n: int, useCalc: bool) = if useCalc then $"%i{n} calc" else $"%i{n}"

        let case =
            match msg with
            | ChangeComponent s -> $"ChangeComponent %s{value s}"
            | ChangeItem s -> $"ChangeItem %s{value s}"
            | ChangeComponentOrderableQuantity s -> $"ChangeComponentOrderableQuantity %s{value s}"
            | ChangeFrequency s -> $"ChangeFrequency %s{value s}"
            | ChangeTime s -> $"ChangeTime %s{value s}"
            | ChangeSubstanceDoseQuantity s -> $"ChangeSubstanceDoseQuantity %s{value s}"
            | ChangeSubstanceDoseQuantityAdjust s -> $"ChangeSubstanceDoseQuantityAdjust %s{value s}"
            | ChangeSubstancePerTime s -> $"ChangeSubstancePerTime %s{value s}"
            | ChangeSubstancePerTimeAdjust s -> $"ChangeSubstancePerTimeAdjust %s{value s}"
            | ChangeSubstanceRate s -> $"ChangeSubstanceRate %s{value s}"
            | ChangeSubstanceRateAdjust s -> $"ChangeSubstanceRateAdjust %s{value s}"
            | ChangeSubstanceComponentConcentration(cmp, sbst, s) ->
                $"ChangeSubstanceComponentConcentration %s{cmp} %s{sbst} %s{value s}"
            | ChangeSubstanceOrderableConcentration s -> $"ChangeSubstanceOrderableConcentration %s{value s}"
            | ChangeSubstanceOrderableQuantity s -> $"ChangeSubstanceOrderableQuantity %s{value s}"
            | ChangeOrderableDoseQuantity s -> $"ChangeOrderableDoseQuantity %s{value s}"
            | ChangeOrderableDoseRate s -> $"ChangeOrderableDoseRate %s{value s}"
            | ChangeOrderableQuantity s -> $"ChangeOrderableQuantity %s{value s}"
            | UpdateOrderScenario ord -> $"UpdateOrderScenario %s{Trail.Part.shortId ord.Id}"
            | ResetOrderScenario -> "ResetOrderScenario"
            | DecreaseFrequencyProperty -> "DecreaseFrequencyProperty"
            | IncreaseFrequencyProperty -> "IncreaseFrequencyProperty"
            | SetMinFrequencyProperty -> "SetMinFrequencyProperty"
            | SetMaxFrequencyProperty -> "SetMaxFrequencyProperty"
            | SetMedianFrequencyProperty -> "SetMedianFrequencyProperty"
            | DecreaseDoseQuantityProperty(n, uc) -> $"DecreaseDoseQuantityProperty %s{steps (n, uc)}"
            | IncreaseDoseQuantityProperty(n, uc) -> $"IncreaseDoseQuantityProperty %s{steps (n, uc)}"
            | SetMinDoseQuantityProperty -> "SetMinDoseQuantityProperty"
            | SetMaxDoseQuantityProperty -> "SetMaxDoseQuantityProperty"
            | SetMedianDoseQuantityProperty -> "SetMedianDoseQuantityProperty"
            | DecreaseDoseRateProperty(n, uc) -> $"DecreaseDoseRateProperty %s{steps (n, uc)}"
            | IncreaseDoseRateProperty(n, uc) -> $"IncreaseDoseRateProperty %s{steps (n, uc)}"
            | SetMinDoseRateProperty -> "SetMinDoseRateProperty"
            | SetMaxDoseRateProperty -> "SetMaxDoseRateProperty"
            | SetMedianDoseRateProperty -> "SetMedianDoseRateProperty"
            | DecreaseComponentQuantityProperty(n, uc) -> $"DecreaseComponentQuantityProperty %s{steps (n, uc)}"
            | IncreaseComponentQuantityProperty(n, uc) -> $"IncreaseComponentQuantityProperty %s{steps (n, uc)}"
            | SetMinComponentQuantityProperty -> "SetMinComponentQuantityProperty"
            | SetMaxComponentQuantityProperty -> "SetMaxComponentQuantityProperty"
            | SetMedianComponentQuantityProperty -> "SetMedianComponentQuantityProperty"

        match msgToField msg with
        | Some field -> $"%s{case} field %s{field}"
        | None -> case


    /// The call a message makes, given the order shown, whether a reopen is under way and the dialog's state.
    let private effectsOf (shown: Order option) (reopening: bool) (state: State) msg =
        let shownId = shown |> Option.map (_.Id >> Trail.Part.shortId)

        match kindOf msg, shownId with
        | Kind.Local, _ -> []
        | Kind.Update, _ ->
            match msg with
            | UpdateOrderScenario ord ->
                let call = if reopening then "CallReopen" else "CallUpdate"
                [ $"%s{call} %s{Trail.Part.shortId ord.Id}" ]
            | _ -> []
        | _, None -> []
        | Kind.Reset, Some id -> [ $"CallReset %s{id}" ]
        | Kind.Change, Some _ -> [ "ofMsg UpdateOrderScenario" ]
        | Kind.Step step, Some _ ->
            match msgToField msg with
            // a component quantity step without a component selected makes no call
            | Some "compOrdQty" when state.SelectedComponent.IsNone -> []
            | field ->
                let field = field |> Option.defaultValue "?"
                [ $"CallStep %s{field} %s{step}" ]


    let private describeState (state: State) =
        let value (s: string option) = s |> Option.defaultValue "none"
        $"cmp %s{value state.SelectedComponent} item %s{value state.SelectedItem}"


    [<JSX.Component>]
    let View
        (props:
            {|
                orderContext: OrderContextView
                updateOrderScenario: OrderContext -> unit
                // a clear from a field's arrow, with the picks: the page sends it and keeps what it
                // showed before
                reopenOrderScenario: string[] -> OrderContext -> unit
                // the list of a reopen closed without a pick: the page puts back what it showed
                restoreOrderScenario: unit -> unit
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
                // the argumentation typed, committed when the field loses focus; the page
                // writes it on its lane
                argue: string -> unit
                localizationTerms: Deferred<string[][]>
                // what the user can change: everything on the workbench, less in the plan
                editing: PlanContextPolicy.Editing
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
            | OrderContextView.Refused(ctx, _)
            | OrderContextView.Changing ctx -> Some ctx
            | OrderContextView.NoPatient -> None

        let shownOrder =
            shownContext
            |> Option.bind (fun ctx -> ctx.Scenarios |> Array.tryExactlyOne |> Option.map _.Order)

        let useAdjust =
            shownContext
            |> Option.bind (fun pr -> pr.Scenarios |> Array.tryExactlyOne |> Option.map _.UseAdjust)
            |> Option.defaultValue false

        // set by a field's arrow just before it clears its value, so that the change goes out as
        // a reopen rather than as a change
        let reopening = React.useRef false

        // the variables the user picked or stepped in the workbench, in the order picked; a new
        // order starts with none; the plan dialog keeps no picks, so its reopen is a plain clear,
        // and a value another variable pins comes back as it was
        let initialPicks =
            if props.editing = PlanContextPolicy.Editing.Workbench then
                Some [||]
            else
                None

        let picks, setPicks = React.useState<string[] option> initialPicks

        // the picks before a reopen, put back when its list closes without a pick
        let heldPicks = React.useRef<string[] option> None

        // the variable a step moves and the order it started from, until its answer lands
        let stepping = React.useRef<(string * Order) option> None

        // a new order, also after a patient change or a resource sync, starts from the initial picks
        React.useEffect ((fun () -> setPicks initialPicks), [| box (shownOrder |> Option.map _.Id) |])

        // a change goes out and adds what it picked to the picks; a reopen goes with the picks from
        // before it, and the server keeps those made before the cleared one
        let updateOrderScenario (ol: OrderLoader) =
            match props.orderContext with
            | OrderContextView.Settled ctx
            | OrderContextView.Refused(ctx, _)
            | OrderContextView.Changing ctx ->
                let isReopen = reopening.current
                reopening.current <- false

                if not isReopen then
                    shownOrder
                    |> Option.iter (fun before -> setPicks (picks |> PickList.afterChange before ol.Order))

                ViewHelpers.withLoader ctx ol
                |> if isReopen then
                       props.reopenOrderScenario (heldPicks.current |> Option.defaultValue [||])
                   else
                       props.updateOrderScenario
            | _ -> ()

        // a reset keeps the order id, so it starts from the initial picks itself
        let resetOrderScenario (ol: OrderLoader) =
            match props.orderContext with
            | OrderContextView.Settled ctx
            | OrderContextView.Refused(ctx, _)
            | OrderContextView.Changing ctx ->
                setPicks initialPicks

                ViewHelpers.withLoader ctx ol |> props.refreshOrderScenario
            | _ -> ()

        let stepper =
            // the variable each stepper moves, by name
            let frequencyOf (ol: OrderLoader) = Some ol.Order.Schedule.Frequency.Name
            let doseQuantityOf (ol: OrderLoader) = Some ol.Order.Orderable.Dose.Quantity.Name
            let doseRateOf (ol: OrderLoader) = Some ol.Order.Orderable.Dose.Rate.Name

            let componentQuantityOf (ol: OrderLoader) =
                ol.Order.Orderable.Components
                |> Array.tryFind (fun c -> ol.Component = Some c.Name)
                |> Option.map _.OrderableQuantity.Name

            // a step notes the variable it moves, so its answer can add it to the picks
            let stepped nameOf (ol: OrderLoader) =
                stepping.current <- nameOf ol |> Option.map (fun name -> name, ol.Order)

            let create nameOf nav =
                fun (ol: OrderLoader) ->
                    stepped nameOf ol

                    match props.orderContext with
                    | OrderContextView.Settled ctx
                    | OrderContextView.Refused(ctx, _)
                    | OrderContextView.Changing ctx -> ViewHelpers.withLoader ctx ol |> nav
                    | _ -> ()

            let createWithCmp nameOf nav =
                fun (ol: OrderLoader) ->
                    stepped nameOf ol

                    match props.orderContext with
                    | OrderContextView.Settled ctx
                    | OrderContextView.Refused(ctx, _)
                    | OrderContextView.Changing ctx ->
                        match ol.Component with
                        | None -> ()
                        | Some cmp ->
                            let ctx = ViewHelpers.withLoader ctx ol

                            nav (ctx, cmp)
                    | _ -> ()

            let createWithN nameOf nav =
                fun (n, uc) (ol: OrderLoader) ->
                    stepped nameOf ol

                    match props.orderContext with
                    | OrderContextView.Settled ctx
                    | OrderContextView.Refused(ctx, _)
                    | OrderContextView.Changing ctx ->
                        match ol.Component with
                        | None -> ()
                        | Some _ ->
                            let ctx = ViewHelpers.withLoader ctx ol

                            nav (ctx, n, uc)
                    | _ -> ()

            let createWithCmpN nameOf nav =
                fun (n, uc) (ol: OrderLoader) ->
                    stepped nameOf ol

                    match props.orderContext with
                    | OrderContextView.Settled ctx
                    | OrderContextView.Refused(ctx, _)
                    | OrderContextView.Changing ctx ->
                        match ol.Component with
                        | None -> ()
                        | Some cmp ->
                            let ctx = ViewHelpers.withLoader ctx ol

                            nav (ctx, cmp, n, uc)
                    | _ -> ()

            {|
                // Frequency
                setFreqMin = create frequencyOf props.stepOrderScenario.setMinFrequency
                setFreqDec = create frequencyOf props.stepOrderScenario.decrFrequency
                setFreqMed = create frequencyOf props.stepOrderScenario.setMedianFrequency
                setFreqInc = create frequencyOf props.stepOrderScenario.incrFrequency
                setFreqMax = create frequencyOf props.stepOrderScenario.setMaxFrequency
                // Dose Rate
                setRateMin = create doseRateOf props.stepOrderScenario.setMinRate
                setRateDec = createWithN doseRateOf props.stepOrderScenario.decrRate
                setRateMed = create doseRateOf props.stepOrderScenario.setMedianRate
                setRateInc = createWithN doseRateOf props.stepOrderScenario.incrRate
                setRateMax = create doseRateOf props.stepOrderScenario.setMaxRate
                // Dose Quantity
                setDoseQtyMin = create doseQuantityOf props.stepOrderScenario.setMinDoseQty
                setDoseQtyDec = createWithN doseQuantityOf props.stepOrderScenario.decrDoseQty
                setDoseQtyMed = create doseQuantityOf props.stepOrderScenario.setMedianDoseQty
                setDoseQtyInc = createWithN doseQuantityOf props.stepOrderScenario.incrDoseQty
                setDoseQtyMax = create doseQuantityOf props.stepOrderScenario.setMaxDoseQty
                // Component Quantity
                setComponentQtyMin = createWithCmp componentQuantityOf props.stepOrderScenario.setMinComponentQty
                setComponentQtyDec = createWithCmpN componentQuantityOf props.stepOrderScenario.decrComponentQty
                setComponentQtyMed = createWithCmp componentQuantityOf props.stepOrderScenario.setMedianComponentQty
                setComponentQtyInc = createWithCmpN componentQuantityOf props.stepOrderScenario.incrComponentQty
                setComponentQtyMax = createWithCmp componentQuantityOf props.stepOrderScenario.setMaxComponentQty
            |}

        // the reopen flag is read before the update, which resets it
        let updateTraced msg state =
            let reopen = reopening.current
            let next, cmd = update updateOrderScenario resetOrderScenario stepper shownOrder msg state

            StepTrail.record (fun no at ->
                Trail.step "Order" describeMsg id describeState no at msg (next, effectsOf shownOrder reopen next msg)
            )

            next, cmd

        let state, dispatch =
            React.useElmish (init props.orderContext, updateTraced, [| box props.orderContext |])

        // the field whose change went out, and whether it shows that it is loading
        let changing, setChanging = React.useState<(string * bool) option> None

        // the order a reopen started from, shown until its answer lands: the order sent has the
        // reopened value cleared, and a field without values is not drawn, which would take the
        // field and its open list away while the server answers
        let reopenedFrom, setReopenedFrom = React.useState<Order option> None

        // Clear the field changing when parent finishes recalculating
        React.useEffect (
            (fun () ->
                match props.orderContext with
                | OrderContextView.Settled ctx ->
                    setChanging None
                    setReopenedFrom None

                    match stepping.current, ctx.Scenarios |> Array.tryExactlyOne with
                    | Some(name, before), Some sc -> setPicks (picks |> PickList.afterStep name before sc.Order)
                    | _ -> ()

                    stepping.current <- None
                | OrderContextView.Refused _ ->
                    setChanging None
                    setReopenedFrom None
                    stepping.current <- None
                | _ -> ()
            ),
            [| box props.orderContext |]
        )

        // a reopen stays loading until its answer is shown: the order it started from is shown one
        // render past the answer, until the effect above lets go of it
        let isOrderLoading =
            reopenedFrom.IsSome
            || match props.orderContext with
               | OrderContextView.Changing _ -> true
               | OrderContextView.NoPatient
               | OrderContextView.Refused _
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
            | OrderContextView.Settled _
            | OrderContextView.Refused _ -> revisionRef.current <- revisionRef.current + 1
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
        // populated while the server is processing; during a reopen the one it started from
        let displayOrder = reopenedFrom |> Option.orElse shownOrder

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

        // the preparation variables the five preparation fields will show, under the conditions
        // the fields apply themselves, so the heading shows exactly when a preparation field
        // shows: a field without values and without steps renders nothing
        let preparationVariables: OrderVariable list =
            match displayOrder with
            | None -> []
            | Some ord ->
                let multiComponent = ord.Orderable.Components |> Array.length > 1

                let selectedCmp =
                    ord.Orderable.Components
                    |> Array.tryFind (fun c -> state.SelectedComponent.IsNone || c.Name = state.SelectedComponent.Value)

                let hasVals (ovar: OrderVariable) =
                    ovar.Variable.Vals |> Option.exists (fun v -> v.Value |> Array.isEmpty |> not)

                let hasSteps (mode: QuantityModePolicy.Mode) =
                    match mode with
                    | QuantityModePolicy.Mode.Navigable
                    | QuantityModePolicy.Mode.Stepable -> true
                    | QuantityModePolicy.Mode.Selectable
                    | QuantityModePolicy.Mode.Fixed -> false

                // component orderable quantity: more than one component, with values, a range shown
                // as one, or steps; the field shows a range as one entry where the others show none
                let compOrdQty =
                    if not multiComponent then
                        None
                    else
                        selectedCmp
                        |> Option.map _.OrderableQuantity
                        |> Option.filter (fun ovar ->
                            ovar |> ViewHelpers.ovarValsWithRange string 3 |> Array.isEmpty |> not
                            || ovar
                               |> QuantityModePolicy.decideFor QuantityModePolicy.Field.ComponentQuantity ord
                               |> hasSteps
                        )

                // substance component concentration: the selected item, else the component's own
                // item, when its defined values are more than one and it has values
                let substCompConc =
                    let concOf (itm: Item) =
                        itm.ComponentConcentration
                        |> Some
                        |> Option.filter (fun ovar ->
                            ovar.DefinedConstraints.Vals
                            |> Option.exists (fun vu -> vu.Value |> Array.length > 1)
                            && hasVals ovar
                        )

                    match substIndx |> Option.bind (fun i -> itms |> Array.tryItem i) with
                    | Some itm -> concOf itm
                    | None ->
                        selectedCmp
                        |> Option.bind (fun cmp -> cmp.Items |> Array.tryFind (fun i -> i.Name = cmp.Name))
                        |> Option.bind concOf

                // substance orderable quantity: continuous, more than one component, with values
                let substOrbQty =
                    match substIndx with
                    | Some i when multiComponent && ord.Schedule.IsContinuous && itms |> Array.length > 0 ->
                        itms[i].OrderableQuantity |> Some |> Option.filter hasVals
                    | _ -> None

                // substance orderable concentration: not continuous, more than one component, with values
                let substOrbConc =
                    match substIndx with
                    | Some i when multiComponent && ord.Schedule.IsContinuous |> not && itms |> Array.length > 0 ->
                        itms[i].OrderableConcentration |> Some |> Option.filter hasVals
                    | _ -> None

                // orderable quantity: more than one component, with values
                let orbQty =
                    if multiComponent then
                        ord.Orderable.OrderableQuantity |> Some |> Option.filter hasVals
                    else
                        None

                [ compOrdQty; substCompConc; substOrbQty; substOrbConc; orbQty ]
                |> List.choose id

        let showPrepDivider = preparationVariables |> List.isEmpty |> not

        // the preparation section folds to its heading once every value it shows holds one value;
        // the user opens and folds it in between, and an answer that changes the solved state puts
        // it back under the rule. Another order shown by this component starts its fold anew.
        let preparationSolved = preparationVariables |> SectionFoldPolicy.allSolved
        let shownOrderId = displayOrder |> Option.map _.Id |> Option.defaultValue ""
        let fold, setFold = React.useState (SectionFoldPolicy.initial preparationSolved)
        let foldOrderIdRef = React.useRef shownOrderId

        React.useEffect (
            (fun () ->
                if foldOrderIdRef.current <> shownOrderId then
                    foldOrderIdRef.current <- shownOrderId
                    setFold (SectionFoldPolicy.initial preparationSolved)
                else
                    setFold (fold |> SectionFoldPolicy.observe preparationSolved)
            ),
            [| box shownOrderId; box preparationSolved |]
        )

        let toggleFold = fun () -> setFold (fold |> SectionFoldPolicy.toggle)

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

        let markOf = ViewHelpers.markOf

        // the component and the item selects are the dialog's own, never a request; the dialog
        // always has one of each, so neither can be cleared
        let texts = ViewHelpers.quantityFieldTexts getTerm

        let pick = ViewHelpers.orderFixed texts false false

        // a field's select: rests while another field is changing, shows it while its own is; a
        // field the editing does not let change shows its value, without steps or a dropdown
        // a field reopens by its arrow when the user constrained its variable, by name; the reopen
        // keeps the picks made before it, and a list closed without a pick puts them all back
        let reopenOf (name: string) : ViewHelpers.Reopen =
            {|
                constrained = PickList.constrained picks name
                reopening =
                    fun () ->
                        heldPicks.current <- picks
                        setPicks (picks |> PickList.beforeReopen name)
                        reopening.current <- true
                        setReopenedFrom shownOrder
                restore =
                    fun () ->
                        setPicks heldPicks.current
                        // the reopen ends here: its order is no longer held, whatever the answer
                        setReopenedFrom None
                        props.restoreOrderScenario ()
                busy = isOrderLoading
            |}

        let selectFor field (name: string) lbl selected updateSelected mode mark minWidth xs =
            if PlanContextPolicy.canEdit props.editing field then
                ViewHelpers.orderSelect
                    texts
                    false
                    (rests field)
                    (isFieldLoading field)
                    lbl
                    selected
                    updateSelected
                    mode
                    mark
                    (reopenOf name)
                    minWidth
                    xs
            else
                // several values and none picked would show blank in a field that cannot open:
                // the values show as one range, from the first to the last
                let shown =
                    if xs |> Array.length > 1 && selected |> Option.isNone then
                        [| "fixed", $"%s{xs |> Array.head |> snd} – %s{xs |> Array.last |> snd}" |]
                    else
                        xs

                ViewHelpers.orderFixed
                    texts
                    false
                    true
                    false
                    lbl
                    selected
                    ignore
                    Components.QuantityField.Fixed
                    mark
                    minWidth
                    shown

        let argues = props.editing |> PlanContextPolicy.argues


        let fixPrecision = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision

        let onClickOk = fun () -> props.closeOrder ()

        // the argumentation: the text the context holds, typed here and committed when the
        // field loses focus, so that a blur before Ok lands it; the draft follows the context
        let heldArgumentation = shownContext |> Option.bind _.Argumentation |> Option.defaultValue ""

        let argumentation, setArgumentation = React.useState heldArgumentation

        React.useEffect ((fun () -> setArgumentation heldArgumentation), [| box heldArgumentation |])

        let onArgumentation (e: Browser.Types.Event) = setArgumentation (e.target?value: string)

        // a locked context commits nothing: its argumentation is shown, not edited
        let onArgumentationBlur =
            fun _ ->
                if argues then
                    props.argue argumentation

        // Escape closes the dialog without a blur, so a draft not yet committed goes with the
        // dialog when it unmounts; through refs, since the cleanup runs with the first
        // render's values otherwise
        let draftRef = React.useRef argumentation
        draftRef.current <- argumentation
        let heldRef = React.useRef heldArgumentation
        heldRef.current <- heldArgumentation
        let argueRef = React.useRef props.argue
        argueRef.current <- props.argue
        let arguesRef = React.useRef argues
        arguesRef.current <- argues

        React.useEffectOnce (fun () ->
            fun () ->
                if arguesRef.current && draftRef.current <> heldRef.current then
                    argueRef.current draftRef.current
        )

        let argumentationWanted = shownContext |> Option.exists ArgumentationPolicy.wanted

        // Ok completes the dialog and Reset discards the changes: the bar places them. Reset is
        // the workbench's alone: it re-solves from the rules, which in the plan could change
        // what the plan context rule keeps fixed
        let resetAction =
            {|
                label = Terms.Reset |> getTerm "Reset"
                kind = Components.ActionBar.Kind.Secondary
                onClick = fun () -> ResetOrderScenario |> dispatch
                disabled = isOrderLoading
                icon = Some Mui.Icons.RefreshIcon
            |}

        let actionBar =
            Components.ActionBar.View
                {|
                    actions =
                        [|
                            if props.editing |> PlanContextPolicy.resets then
                                resetAction
                            {|
                                label = Terms.``Ok `` |> getTerm "Ok"
                                kind = Components.ActionBar.Kind.Primary
                                onClick = onClickOk
                                disabled = false
                                icon = None
                            |}
                        |]
                |}

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

        // the three sections of the dialog, each headed when it has a field to show
        let heading label =
            Components.SectionHeading.View
                {|
                    label = label
                    action = None
                |}

        let dosingDivider = if showDosingDivider then heading "dosering" else null

        let administrationDivider =
            if showAdminDivider then
                Terms.``Prescribe Administration`` |> getTerm "toediening" |> heading
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
                        |> pick
                            false
                            "componenten"
                            state.SelectedComponent
                            (ChangeComponent >> dispatch)
                            ViewHelpers.noSteps
                            ViewHelpers.noMark
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
                        |> pick
                            false
                            "stoffen"
                            state.SelectedItem
                            (ChangeItem >> dispatch)
                            ViewHelpers.noSteps
                            ViewHelpers.noMark
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

                    let severity = itms[i].Dose.Quantity |> markOf

                    vals
                    |> selectFor
                        "substDoseQty"
                        itms[i].Dose.Quantity.Name
                        label
                        None
                        (ChangeSubstanceDoseQuantity >> dispatch)
                        ViewHelpers.noSteps
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

                    let severity = itms[i].Dose.QuantityAdjust |> markOf

                    vals
                    |> selectFor
                        "substDoseQtyAdj"
                        itms[i].Dose.QuantityAdjust.Name
                        label
                        None
                        (ChangeSubstanceDoseQuantityAdjust >> dispatch)
                        ViewHelpers.noSteps
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
                            itms[i].Dose.PerTimeAdjust |> markOf
                        else
                            itms[i].Dose.PerTime |> markOf

                    vals
                    |> selectFor
                        "substPerTime"
                        (if useAdjust then
                             itms[i].Dose.PerTimeAdjust.Name
                         else
                             itms[i].Dose.PerTime.Name)
                        label
                        None
                        dispatch
                        ViewHelpers.noSteps
                        severity
                        None
                | _ -> null

            let substRateSelect =
                let stepper = ViewHelpers.noSteps

                match substIndx, displayOrder with
                | Some i, Some ord when ord.Schedule.IsContinuous && itms |> Array.length > 0 ->
                    let dispatch =
                        if useAdjust then
                            ChangeSubstanceRateAdjust >> dispatch
                        else
                            ChangeSubstanceRate >> dispatch

                    let severity =
                        if useAdjust then
                            itms[i].Dose.RateAdjust |> markOf
                        else
                            itms[i].Dose.Rate |> markOf

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
                        ovar.Name
                        (Terms.``Order Adjusted dose`` |> getTerm "dosering")
                        None
                        dispatch
                        stepper
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
                        match cmp with
                        | None -> ViewHelpers.noSteps
                        | Some cmp ->
                            let mode =
                                cmp.OrderableQuantity
                                |> QuantityModePolicy.decideFor QuantityModePolicy.Field.ComponentQuantity ord

                            createStepper
                                mode
                                (cmp.OrderableQuantity |> ViewHelpers.hasLargeStep)
                                SetMinComponentQuantityProperty
                                DecreaseComponentQuantityProperty
                                SetMedianComponentQuantityProperty
                                IncreaseComponentQuantityProperty
                                SetMaxComponentQuantityProperty
                                (cmp.OrderableQuantity |> ViewHelpers.ovarStep string)
                                (cmp.OrderableQuantity |> ViewHelpers.largeStepText)

                    let severity =
                        cmp
                        |> Option.map (_.OrderableQuantity >> markOf)
                        |> Option.defaultValue ViewHelpers.noMark

                    vals
                    |> selectFor
                        "compOrdQty"
                        (cmp |> Option.map _.OrderableQuantity.Name |> Option.defaultValue "")
                        "bereiding hoeveelheid"
                        None
                        (ChangeComponentOrderableQuantity >> dispatch)
                        stepper
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
                                itm.ComponentConcentration.Name
                                "product sterkte"
                                None
                                (change >> dispatch)
                                ViewHelpers.noSteps
                                ViewHelpers.noMark
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
                                        itm.ComponentConcentration.Name
                                        "product sterkte"
                                        None
                                        (change >> dispatch)
                                        ViewHelpers.noSteps
                                        ViewHelpers.noMark
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
                    let severity = itms[i].OrderableQuantity |> markOf

                    itms[i].OrderableQuantity
                    |> ViewHelpers.ovarVals (fixPrecision 3)
                    |> selectFor
                        "substOrdQty"
                        itms[i].OrderableQuantity.Name
                        $"{itms[i].Name} hoeveelheid"
                        None
                        (ChangeSubstanceOrderableQuantity >> dispatch)
                        ViewHelpers.noSteps
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
                    let severity = itms[i].OrderableConcentration |> markOf

                    itms[i].OrderableConcentration
                    |> ViewHelpers.ovarVals (fixPrecision 3)
                    |> selectFor
                        "substOrdConc"
                        itms[i].OrderableConcentration.Name
                        $"{itms[i].Name} concentratie"
                        None
                        (ChangeSubstanceOrderableConcentration >> dispatch)
                        ViewHelpers.noSteps
                        severity
                        None
                | _ -> null

            let ordQtySelect =
                match displayOrder with
                | Some ord when ord.Orderable.Components |> Array.length > 1 ->
                    let severity = ord.Orderable.OrderableQuantity |> markOf

                    ord.Orderable.OrderableQuantity
                    |> ViewHelpers.ovarVals string
                    |> selectFor
                        "ordQty"
                        ord.Orderable.OrderableQuantity.Name
                        "totale hoeveelheid"
                        None
                        (ChangeOrderableQuantity >> dispatch)
                        ViewHelpers.noSteps
                        severity
                        None
                | _ -> null

            let frequencySelect =
                match displayOrder with
                | Some ord when ord.Schedule.IsDiscontinuous || ord.Schedule.IsTimed ->
                    let xs = ord.Schedule.Frequency |> ViewHelpers.ovarValsWithRange string 3

                    let stepper =
                        let mode =
                            ord.Schedule.Frequency
                            |> QuantityModePolicy.decideFor QuantityModePolicy.Field.Frequency ord

                        ViewHelpers.frequencyStepper
                            dispatch
                            revision
                            mode
                            SetMinFrequencyProperty
                            DecreaseFrequencyProperty
                            SetMedianFrequencyProperty
                            IncreaseFrequencyProperty
                            SetMaxFrequencyProperty

                    let severity = ord.Schedule.Frequency |> markOf

                    selectFor
                        "frequency"
                        ord.Schedule.Frequency.Name
                        (Terms.``Order Frequency`` |> getTerm "frequentie")
                        None
                        (ChangeFrequency >> dispatch)
                        stepper
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

                    let severity = ord.Orderable.Dose.Quantity |> markOf

                    ord.Orderable.Dose.Quantity
                    |> ViewHelpers.ovarValsWithRange string 3
                    |> selectFor
                        "ordDoseQty"
                        ord.Orderable.Dose.Quantity.Name
                        "toedien hoeveelheid"
                        None
                        (ChangeOrderableDoseQuantity >> dispatch)
                        stepper
                        severity
                        None
                | _ -> null

            let ordDoseRateSelect =
                match displayOrder with
                | Some ord when ord.Schedule.IsContinuous || ord.Schedule.IsTimed || ord.Schedule.IsOnceTimed ->
                    let stepper =
                        let mode =
                            ord.Orderable.Dose.Rate
                            |> QuantityModePolicy.decideFor QuantityModePolicy.Field.DoseRate ord

                        ViewHelpers.doseRateStepper
                            dispatch
                            revision
                            mode
                            ord.Orderable.Dose.Rate
                            SetMinDoseRateProperty
                            DecreaseDoseRateProperty
                            SetMedianDoseRateProperty
                            IncreaseDoseRateProperty
                            SetMaxDoseRateProperty

                    let severity = ord.Orderable.Dose.Rate |> markOf

                    ord.Orderable.Dose.Rate
                    |> ViewHelpers.ovarValsWithRange string 3
                    |> selectFor
                        "ordDoseRate"
                        ord.Orderable.Dose.Rate.Name
                        (Terms.``Order Drip rate`` |> getTerm "inloop snelheid")
                        None
                        (ChangeOrderableDoseRate >> dispatch)
                        stepper
                        severity
                        None
                | _ -> null

            let timeSelect =
                match displayOrder with
                | Some ord ->
                    let severity = ord.Schedule.Time |> markOf

                    ord.Schedule.Time
                    |> ViewHelpers.ovarVals (fixPrecision 2)
                    |> Array.distinctBy snd
                    |> selectFor
                        "time"
                        ord.Schedule.Time.Name
                        (Terms.``Order Administration time`` |> getTerm "inloop tijd")
                        None
                        (ChangeTime >> dispatch)
                        ViewHelpers.noSteps
                        severity
                        None
                | _ -> null

            // the preparation section: its heading like the other sections', with the button that
            // opens or folds it beside the name, and its five fields below while it is open
            let preparationOpen = showPrepDivider && (fold |> SectionFoldPolicy.isOpen)

            let preparationDivider =
                if not showPrepDivider then
                    null
                else
                    let icon =
                        if preparationOpen then
                            Mui.Icons.ExpandLessIcon
                        else
                            Mui.Icons.ExpandMoreIcon

                    let onClick = fun _ -> toggleFold ()

                    let label = Terms.``Prescribe Preparation`` |> getTerm "bereiding"

                    // the button is named after the section it opens and folds, since its icon
                    // says nothing to a screen reader and the name beside it is not linked to it
                    let foldButton =
                        JSX.jsx
                            $"""
                        import IconButton from '@mui/material/IconButton';

                        <IconButton size="small" onClick={onClick} aria-label={label} aria-expanded={preparationOpen}>
                            {icon}
                        </IconButton>
                        """

                    Components.SectionHeading.View
                        {|
                            label = label
                            action = Some foldButton
                        |}

            let whileOpen field = if preparationOpen then field else null

            // the argumentation, last: shown when the rules mark the order or a text is there,
            // capped where the policy caps it
            let argumentationField =
                if not argumentationWanted then
                    null
                else
                    let label = Terms.``Order Argumentation`` |> getTerm "argumentatie"

                    let helper =
                        Terms.``Order Argumentation Helper``
                        |> getTerm "waarom de dosering afwijkt van wat de regels toestaan"

                    let inputProps =
                        {|
                            htmlInput =
                                {|
                                    maxLength = ArgumentationPolicy.maxLength
                                    readOnly = not argues
                                |}
                        |}

                    JSX.jsx
                        $"""
                    import TextField from '@mui/material/TextField';

                    <TextField
                        id="order-argumentation"
                        label={label}
                        helperText={helper}
                        multiline={true}
                        minRows={2}
                        fullWidth={true}
                        variant="outlined"
                        value={argumentation}
                        onChange={onArgumentation}
                        onBlur={onArgumentationBlur}
                        slotProps={inputProps}
                    />
                    """

            // The fields the dialog shows, in the order it shows them: the case per dose type the
            // client hard-codes today, as one literal at the call site. Each field decides for
            // itself whether it applies to the order and renders nothing otherwise; none is the
            // lead the user is pointed at first, since which field that is, and in what order
            // they come, is the server's to send with the order and replaces this literal.
            let fields =
                [|
                    componentSelect
                    itemSelect
                    dosingDivider
                    substDoseQtySelect
                    substDoseQtyAdjSelect
                    substPerTimeSelect
                    substRateSelect
                    preparationDivider
                    whileOpen compOrdQtySelect
                    whileOpen substCompConcSelect
                    whileOpen substOrdQtySelect
                    whileOpen substOrdConcSelect
                    whileOpen ordQtySelect
                    administrationDivider
                    frequencySelect
                    ordDoseQtySelect
                    ordDoseRateSelect
                    timeSelect
                    argumentationField
                |]
                |> unbox<seq<ReactElement>>
                |> React.Fragment

            // the spinner lies over the fields and takes no room, so the dialog keeps its height
            // and the fields stay where they are while the order reloads
            let fieldsOverlay =
                let stack =
                    JSX.jsx
                        $"""
                    import Stack from '@mui/material/Stack';
                    <Stack direction={"column"} spacing={if isMobile then 1.5 else 3} >
                        {fields}
                    </Stack>
                    """

                Components.LoadingOverlay.View
                    {|
                        isLoading = isOrderLoading
                        children = stack
                    |}

            let titleSlotProps = {| title = {| variant = "h6" |} |}

            JSX.jsx
                $"""
            import CardHeader from '@mui/material/CardHeader';
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';
            import Paper from '@mui/material/Paper';
            import Divider from '@mui/material/Divider';
            import Box from '@mui/material/Box';
            <div>
            <CardHeader
                sx = {headerSx}
                title={displayOrder |> showOrderName}
                slotProps={titleSlotProps}
            ></CardHeader>
            <CardContent sx={contentSx}>
                {fieldsOverlay}
            </CardContent>
            <CardActions >
                {actionBar}
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
