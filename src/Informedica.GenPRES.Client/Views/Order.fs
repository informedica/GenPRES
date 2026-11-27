namespace Views


module Trail = Informedica.GenPRES.Client.Core.Trail


module Order =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines

    open Fable.Core
    open Fable.Core.JsInterop
    open Fable.React
    open Feliz
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models.Order
    open Informedica.GenPRES.Shared
    open OrderContextMachine
    open Elmish
    open FSharp.Core


    module private Elmish =


        /// The component and the item picked in the dialog. The order comes from the context shown.
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
            | UpdateOrderScenario of Models.OrderContext.Target option * string option
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
            // the dose quantity as a share of the full intake, in percent
            | SetDoseQuantityPercProperty of perc: int
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


        /// Starts with the component and the item of the single scenario of the context shown: those of the
        /// last render when it showed the same order, else the ones DialogTabPolicy.tab chooses.
        let init (kept: DialogTabPolicy.Tab option) (ctx: OrderContextView) =
            let tab =
                match ctx with
                | OrderContextView.Settled ctx
                | OrderContextView.Refused(ctx, _)
                | OrderContextView.Changing ctx ->
                    ctx.Scenarios |> Array.tryExactlyOne |> Option.map (DialogTabPolicy.tab kept)
                | OrderContextView.NoPatientData -> None

            {
                SelectedComponent = tab |> Option.bind _.Component
                SelectedItem = tab |> Option.bind _.Item
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
                    setDoseQtyPerc: int -> OrderLoader -> unit

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
            // Runs a step on the order shown; does nothing when no order is shown.
            let handleStep step =
                match shown with
                | None -> state, Cmd.none
                | Some ord ->
                    OrderLoader.create state.SelectedComponent state.SelectedItem ord |> step
                    state, Cmd.none

            // Sends a pick of an order variable as UpdateOrderScenario; does nothing when no order is shown.
            let pick target s =
                match shown with
                | Some _ -> state, Cmd.ofMsg (UpdateOrderScenario(target, s))
                | None -> state, Cmd.none

            // the component the dialog shows: the one picked, or the first
            let shownComponent (ord: Order) =
                ord.Orderable.Components
                |> Array.tryFind (fun c -> state.SelectedComponent.IsNone || state.SelectedComponent = Some c.Name)

            // The item the dialog shows: the item picked, else the first item of the component shown that
            // is not an additional item.
            let item prop =
                shown
                |> Option.bind shownComponent
                |> Option.bind (fun cmp ->
                    state.SelectedItem
                    |> Option.orElse (cmp.Items |> Array.tryFind (_.IsAdditional >> not) |> Option.map _.Name)
                    |> Option.map (fun itm -> Models.OrderContext.Target.Item(cmp.Name, itm, prop))
                )

            let schedule prop = Some(Models.OrderContext.Target.Schedule prop)
            let orderable prop = Some(Models.OrderContext.Target.Orderable prop)

            match msg with

            | UpdateOrderScenario(target, s) ->
                match shown with
                | Some ord ->
                    updateOrderScenario (OrderLoader.create state.SelectedComponent state.SelectedItem ord) target s
                | None -> ()

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
                let target =
                    state.SelectedComponent
                    |> Option.map (fun c ->
                        Models.OrderContext.Target.Component(c, ComponentProperty.OrderableQuantity)
                    )

                pick target s

            | ChangeItem itm ->
                match itm with
                | None -> state, Cmd.none
                | Some _ -> { state with SelectedItem = itm }, Cmd.none

            | ChangeFrequency s -> pick (schedule ScheduleProperty.Frequency) s
            | ChangeTime s -> pick (schedule ScheduleProperty.Time) s
            | ChangeSubstanceDoseQuantity s -> pick (item ItemProperty.DoseQuantity) s
            | ChangeSubstanceDoseQuantityAdjust s -> pick (item ItemProperty.DoseQuantityAdjust) s
            | ChangeSubstancePerTime s -> pick (item ItemProperty.DosePerTime) s
            | ChangeSubstancePerTimeAdjust s -> pick (item ItemProperty.DosePerTimeAdjust) s
            | ChangeSubstanceRate s -> pick (item ItemProperty.DoseRate) s
            | ChangeSubstanceRateAdjust s -> pick (item ItemProperty.DoseRateAdjust) s
            // The message names the component and the item itself.
            | ChangeSubstanceComponentConcentration(cname, iname, s) ->
                pick (Some(Models.OrderContext.Target.Item(cname, iname, ItemProperty.ComponentConcentration))) s
            | ChangeSubstanceOrderableConcentration s -> pick (item ItemProperty.OrderableConcentration) s
            | ChangeSubstanceOrderableQuantity s -> pick (item ItemProperty.OrderableQuantity) s
            | ChangeOrderableDoseQuantity s -> pick (orderable OrderableProperty.DoseQuantity) s
            | ChangeOrderableDoseRate s -> pick (orderable OrderableProperty.DoseRate) s
            | ChangeOrderableQuantity s -> pick (orderable OrderableProperty.Quantity) s

            // == Frequency ==
            | SetMinFrequencyProperty -> handleStep stepper.setFreqMin
            | DecreaseFrequencyProperty -> handleStep stepper.setFreqDec
            | SetMedianFrequencyProperty -> handleStep stepper.setFreqMed
            | IncreaseFrequencyProperty -> handleStep stepper.setFreqInc
            | SetMaxFrequencyProperty -> handleStep stepper.setFreqMax
            // == Rate ==
            | SetMinDoseRateProperty -> handleStep stepper.setRateMin
            | DecreaseDoseRateProperty(n, uc) -> handleStep (stepper.setRateDec (n, uc))
            | SetMedianDoseRateProperty -> handleStep stepper.setRateMed
            | IncreaseDoseRateProperty(n, uc) -> handleStep (stepper.setRateInc (n, uc))
            | SetMaxDoseRateProperty -> handleStep stepper.setRateMax
            // == DoseQty ==
            | SetMinDoseQuantityProperty -> handleStep stepper.setDoseQtyMin
            | DecreaseDoseQuantityProperty(n, uc) -> handleStep (stepper.setDoseQtyDec (n, uc))
            | SetMedianDoseQuantityProperty -> handleStep stepper.setDoseQtyMed
            | IncreaseDoseQuantityProperty(n, uc) -> handleStep (stepper.setDoseQtyInc (n, uc))
            | SetMaxDoseQuantityProperty -> handleStep stepper.setDoseQtyMax
            | SetDoseQuantityPercProperty perc -> handleStep (stepper.setDoseQtyPerc perc)
            // == ComponentQty ==
            | SetMinComponentQuantityProperty -> handleStep stepper.setComponentQtyMin
            | DecreaseComponentQuantityProperty(n, uc) -> handleStep (stepper.setComponentQtyDec (n, uc))
            | SetMedianComponentQuantityProperty -> handleStep stepper.setComponentQtyMed
            | IncreaseComponentQuantityProperty(n, uc) -> handleStep (stepper.setComponentQtyInc (n, uc))
            | SetMaxComponentQuantityProperty -> handleStep stepper.setComponentQtyMax


        /// The name of the order and the form of its first component.
        let orderName (ord: Order) =
            let form =
                ord.Orderable.Components
                |> Array.tryHead
                |> Option.map _.Form
                |> Option.defaultValue ""

            $"{ord.Orderable.Name} {form}"


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
        | SetMaxDoseQuantityProperty
        | SetDoseQuantityPercProperty _ -> Some "ordDoseQty"
        | ChangeOrderableDoseRate _
        | SetMinDoseRateProperty
        | DecreaseDoseRateProperty _
        | SetMedianDoseRateProperty
        | IncreaseDoseRateProperty _
        | SetMaxDoseRateProperty -> Some "ordDoseRate"
        | _ -> None


    /// The kind of a message: a change of the dialog alone, a value change, a step, an update or a reset.
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
        | SetDoseQuantityPercProperty _ -> Kind.Step "perc"
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
            | UpdateOrderScenario(_, s) -> $"UpdateOrderScenario %s{value s}"
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
            | SetDoseQuantityPercProperty perc -> $"SetDoseQuantityPercProperty %i{perc}"
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


    /// Lists the calls a message makes for the trail, given the order shown and the dialog state.
    let private effectsOf (shown: Order option) (state: State) msg =
        let shownId = shown |> Option.map (_.Id >> Trail.Part.shortId)

        match kindOf msg, shownId with
        | Kind.Local, _ -> []
        | Kind.Update, _ ->
            match msg with
            | UpdateOrderScenario _ ->
                let id = shownId |> Option.defaultValue "none"
                [ $"CallUpdate %s{id}" ]
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
                // Sends a command from the dialog to the order context of the page.
                command: Api.OrderViewCommand -> unit
                // Called when a recalculation list closes without a pick; the page restores the order it showed.
                restoreOrderScenario: unit -> unit
                closeOrder: unit -> unit
                // True while a request of the page is out or a field counts clicks; the dialog does not close then.
                busy: bool
                localizationTerms: Deferred<string[][]>
                // What the user can change: everything on the workbench; in the plan, the fields the plan context
                // policy allows and the dose share of a TPN when it can be set.
                editing: PlanContextPolicy.Editing
            |})
        =
        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let isMobile = Mui.Hooks.useIsMobile ()

        let getTerm = Global.getLocalizedTerm props.localizationTerms lang
        let productStrength = Terms.``Order Product Strength`` |> getTerm "product strength"

        // The context shown; while a change is under way, the context that was sent.
        let shownContext =
            match props.orderContext with
            | OrderContextView.Settled ctx
            | OrderContextView.Refused(ctx, _)
            | OrderContextView.Changing ctx -> Some ctx
            | OrderContextView.NoPatientData -> None

        // The order of the single scenario of the context shown. While a change is under way it is the order
        // that was sent, so the dialog stays filled while the server answers.
        let shownOrder =
            shownContext
            |> Option.bind (fun ctx -> ctx.Scenarios |> Array.tryExactlyOne |> Option.map _.Order)

        let useAdjust =
            shownContext
            |> Option.bind (fun pr -> pr.Scenarios |> Array.tryExactlyOne |> Option.map _.UseAdjust)
            |> Option.defaultValue false

        // Sends a pick as the index of the value in the order variable. A value the variable does not offer
        // is a bug: it is logged as a warning and not sent.
        let updateOrderScenario (ol: OrderLoader) target (s: string option) =
            match props.orderContext with
            | OrderContextView.Settled _
            | OrderContextView.Refused _
            | OrderContextView.Changing _ ->
                let located =
                    target
                    |> Option.bind (fun t ->
                        ol.Order |> Models.OrderContext.Target.tryGet t |> Option.map (fun v -> t, v)
                    )

                match located, s with
                | Some(t, ovar), Some key ->
                    match Models.OrderContext.values ovar |> Array.tryFindIndex ((=) key) with
                    | Some n -> props.command (ViewHelpers.setNthCommand t n)
                    | None -> Logging.warning "a value the field does not offer is not sent" key
                | _ -> Logging.warning "a change no field holds is not sent" s
            | _ -> ()

        let resetOrderScenario (_: OrderLoader) =
            match props.orderContext with
            | OrderContextView.Settled _
            | OrderContextView.Refused _
            | OrderContextView.Changing _ -> props.command Api.OrderViewCommand.ResetOrderScenario
            | OrderContextView.NoPatientData -> ()

        let stepper =
            // Sends a step command, unless there is no patient data.
            let send cmd =
                match props.orderContext with
                | OrderContextView.NoPatientData -> ()
                | _ -> props.command cmd

            let create cmd = fun (_: OrderLoader) -> send cmd

            let createWithCmp cmd =
                fun (ol: OrderLoader) -> ol.Component |> Option.iter (cmd >> send)

            let createWithN cmd =
                fun (n, uc) (ol: OrderLoader) ->
                    if ol.Component.IsSome then
                        send (cmd (n, uc))

            let createWithCmpN cmd =
                fun (n, uc) (ol: OrderLoader) -> ol.Component |> Option.iter (fun c -> send (cmd (c, n, uc)))

            {|
                // Frequency
                setFreqMin = create Api.OrderViewCommand.SetMinScheduleFrequencyProperty
                setFreqDec = create Api.OrderViewCommand.DecreaseScheduleFrequencyProperty
                setFreqMed = create Api.OrderViewCommand.SetMedianScheduleFrequencyProperty
                setFreqInc = create Api.OrderViewCommand.IncreaseScheduleFrequencyProperty
                setFreqMax = create Api.OrderViewCommand.SetMaxScheduleFrequencyProperty
                // Dose Rate
                setRateMin = create Api.OrderViewCommand.SetMinOrderableDoseRateProperty
                setRateDec = createWithN Api.OrderViewCommand.DecreaseOrderableDoseRateProperty
                setRateMed = create Api.OrderViewCommand.SetMedianOrderableDoseRateProperty
                setRateInc = createWithN Api.OrderViewCommand.IncreaseOrderableDoseRateProperty
                setRateMax = create Api.OrderViewCommand.SetMaxOrderableDoseRateProperty
                // Dose Quantity
                setDoseQtyMin = create Api.OrderViewCommand.SetMinOrderableDoseQuantityProperty
                setDoseQtyDec = createWithN Api.OrderViewCommand.DecreaseOrderableDoseQuantityProperty
                setDoseQtyMed = create Api.OrderViewCommand.SetMedianOrderableDoseQuantityProperty
                setDoseQtyInc = createWithN Api.OrderViewCommand.IncreaseOrderableDoseQuantityProperty
                setDoseQtyMax = create Api.OrderViewCommand.SetMaxOrderableDoseQuantityProperty
                setDoseQtyPerc = fun perc -> create (Api.OrderViewCommand.SetOrderableDoseQuantityPercProperty perc)
                // Component Quantity
                setComponentQtyMin = createWithCmp Api.OrderViewCommand.SetMinComponentOrderableQuantityProperty
                setComponentQtyDec = createWithCmpN Api.OrderViewCommand.DecreaseComponentOrderableQuantityProperty
                setComponentQtyMed = createWithCmp Api.OrderViewCommand.SetMedianComponentOrderableQuantityProperty
                setComponentQtyInc = createWithCmpN Api.OrderViewCommand.IncreaseComponentOrderableQuantityProperty
                setComponentQtyMax = createWithCmp Api.OrderViewCommand.SetMaxComponentOrderableQuantityProperty
            |}

        let updateTraced msg state =
            let next, cmd = update updateOrderScenario resetOrderScenario stepper shownOrder msg state

            StepTrail.record (fun no at ->
                Trail.step "Order" describeMsg id describeState no at msg (next, effectsOf shownOrder next msg)
            )

            next, cmd

        // The component and the item of the last render, so an answer for the same order keeps them.
        let kept = React.useRef<DialogTabPolicy.Tab option> None

        let state, dispatch =
            React.useElmish (init kept.current props.orderContext, updateTraced, [| box props.orderContext |])

        kept.current <-
            shownOrder
            |> Option.map (fun ord ->
                {
                    OrderId = ord.Id
                    Component = state.SelectedComponent
                    Item = state.SelectedItem
                }
                : DialogTabPolicy.Tab
            )

        // the field whose change went out, and whether it shows that it is loading
        let changing, setChanging = React.useState<(string * bool) option> None

        // Clear the field changing when parent finishes recalculating
        React.useEffect (
            (fun () ->
                match props.orderContext with
                | OrderContextView.Settled _
                | OrderContextView.Refused _ -> setChanging None
                | _ -> ()
            ),
            [| box props.orderContext |]
        )

        let isOrderLoading =
            match props.orderContext with
            | OrderContextView.Changing _ -> true
            | OrderContextView.NoPatientData
            | OrderContextView.Refused _
            | OrderContextView.Settled _ -> false

        let isFieldLoading field = isOrderLoading && changing = Some(field, true)


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

        let itms =
            match shownOrder with
            | Some ord ->
                ord.Orderable.Components
                // the component picked, or the first
                |> Array.tryFind (fun cmp -> state.SelectedComponent.IsNone || state.SelectedComponent.Value = cmp.Name)
                |> Option.map (fun cmp ->
                    // filter out additional items, they are not used for dosing
                    cmp.Items |> Array.filter (_.IsAdditional >> not)
                )
                |> Option.defaultValue [||]
            | _ -> [||]

        // The target of a property of item i in the component shown.
        let itemTarget i prop =
            let cmp =
                shownOrder
                |> Option.bind (fun ord ->
                    ord.Orderable.Components
                    |> Array.tryFind (fun c -> state.SelectedComponent.IsNone || state.SelectedComponent = Some c.Name)
                )
                |> Option.map _.Name
                |> Option.defaultValue ""

            Models.OrderContext.Target.Item(cmp, itms[i].Name, prop)

        let substIndx =
            itms
            |> Array.tryFindIndex (fun i -> state.SelectedItem |> Option.map ((=) i.Name) |> Option.defaultValue false)
            |> function
                | None -> Some 0
                | Some i -> Some i

        let showDosingDivider =
            match shownOrder with
            | None -> false
            | Some ord ->
                let hasSubstIndx = substIndx.IsSome && itms |> Array.length > 0

                // Substance dose quantity: shown for a schedule that is not continuous when the item has values.
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

                // Adjusted substance dose quantity: shown for a once or once-timed schedule with an adjusted
                // dose when the item has values.
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

                // Substance dose per time: shown for a schedule that is not continuous when the item has values.
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

                // Substance dose rate: shown for a continuous schedule when the item has values.
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

        // The variables the five preparation fields show, under the same conditions as the fields, so the
        // heading shows exactly when a preparation field shows. A field without values and steps renders nothing.
        let preparationVariables: OrderVariable list =
            match shownOrder with
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

                // Component orderable quantity: shown for more than one component when it has values or steps.
                // This field counts a range as one value, where the other fields count none.
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

                // Substance component concentration of the selected item, else of the item with the name of the
                // component: shown when it has values and more than one defined value.
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

                // Substance orderable quantity: shown for a continuous schedule with more than one component
                // when it has values.
                let substOrbQty =
                    match substIndx with
                    | Some i when multiComponent && ord.Schedule.IsContinuous && itms |> Array.length > 0 ->
                        itms[i].OrderableQuantity |> Some |> Option.filter hasVals
                    | _ -> None

                // Substance orderable concentration: shown for a schedule that is not continuous with more than
                // one component when it has values.
                let substOrbConc =
                    match substIndx with
                    | Some i when multiComponent && ord.Schedule.IsContinuous |> not && itms |> Array.length > 0 ->
                        itms[i].OrderableConcentration |> Some |> Option.filter hasVals
                    | _ -> None

                // Orderable quantity: shown for more than one component when it has values.
                let orbQty =
                    if multiComponent then
                        ord.Orderable.OrderableQuantity |> Some |> Option.filter hasVals
                    else
                        None

                [ compOrdQty; substCompConc; substOrbQty; substOrbConc; orbQty ]
                |> List.choose id

        let showPrepDivider = preparationVariables |> List.isEmpty |> not

        // The preparation section folds to its heading when every value it shows is solved to one value.
        // The user can open and fold it; an answer that changes the solved state applies the rule again.
        // Another order starts with a new fold.
        let preparationSolved = preparationVariables |> SectionFoldPolicy.allSolved
        let shownOrderId = shownOrder |> Option.map _.Id |> Option.defaultValue ""
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
            match shownOrder with
            | None -> false
            | Some ord ->
                // Frequency: shown when it has values.
                let hasFrequency =
                    ord.Schedule.Frequency.Variable.Vals
                    |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                    |> Option.defaultValue false
                // Orderable dose quantity: shown for a schedule that is not continuous when it has values.
                let hasDoseQty =
                    ord.Schedule.IsContinuous |> not
                    && ord.Orderable.Dose.Quantity.Variable.Vals
                       |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       |> Option.defaultValue false
                // Orderable dose rate: shown for a continuous, timed or once-timed schedule. The field always
                // has a stepper, so it renders even without values.
                let hasDoseRate = ord.Schedule.IsContinuous || ord.Schedule.IsTimed || ord.Schedule.IsOnceTimed
                // Administration time: shown when it has values.
                let hasTime =
                    ord.Schedule.Time.Variable.Vals
                    |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                    |> Option.defaultValue false

                hasFrequency || hasDoseQty || hasDoseRate || hasTime

        let markOf = ViewHelpers.markOf

        // The component and the item selects change the dialog alone and send no request. A component and
        // an item are always picked, so neither select can be cleared.
        let texts = ViewHelpers.quantityFieldTexts getTerm

        let pick = ViewHelpers.orderFixed texts false false

        // The select of a field. While a change is under way every select is disabled, and the field that
        // changes shows that it is loading. A field the editing does not allow to change shows its value
        // without steps or a dropdown. A field with one value can be recalculated when the intent policy
        // allows it for the order shown; it shows that it is loading until the answer arrives. A list closed
        // without a pick restores the order shown before.
        let reCalcOf field target : ViewHelpers.ReCalc =
            {|
                allowed = shownOrder |> Option.exists (IntentPolicy.isReCalcAllowed target)
                reCalc =
                    fun () ->
                        setChanging (Some(field, true))
                        props.command (ViewHelpers.reCalcCommand target)
                restore = props.restoreOrderScenario
                busy = isOrderLoading
            |}

        let selectFor field target lbl selected updateSelected mode mark minWidth xs =
            if PlanContextPolicy.canEdit props.editing field then
                ViewHelpers.orderSelect
                    texts
                    false
                    // disabled while a change is under way, so the next change applies to its answer
                    isOrderLoading
                    (isFieldLoading field)
                    lbl
                    selected
                    updateSelected
                    mode
                    mark
                    (reCalcOf field target)
                    minWidth
                    xs
            else
                // Several values with none picked would show blank in a field that cannot open, so the values
                // show as one range from the first to the last.
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


        let fixPrecision = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision

        // The argumentation text of the context. It is edited here and sent as one command when the dialog
        // closes. The draft is reset when the text of the context changes.
        let heldArgumentation = shownContext |> Option.bind _.Argumentation |> Option.defaultValue ""

        let argumentation, setArgumentation = React.useState heldArgumentation

        React.useEffect ((fun () -> setArgumentation heldArgumentation), [| box heldArgumentation |])

        let onArgumentation (e: Browser.Types.Event) = setArgumentation (e.target?value: string)

        let argues = props.editing |> PlanContextPolicy.argues

        // Sends the text only when it differs from the text of the context, because in the plan every
        // command counts as a change. A locked context sends nothing and only shows its text.
        let argue (text: string) =
            if
                argues
                && ArgumentationPolicy.normalise text
                   <> ArgumentationPolicy.normalise heldArgumentation
            then
                props.command (Api.OrderViewCommand.SetArgumentationProperty text)

        // Closing the dialog with Ok, Escape or a click outside first sends a draft not yet sent. A blur
        // sends nothing: pressing Ok blurs the field, and a draft sent then would keep the page busy when
        // the click lands. While the page is busy the dialog stays open and Ok is disabled, so a draft is
        // never sent during another request. A close from outside, when the selection is gone, discards it.
        let close () =
            if not props.busy then
                argue argumentation
                props.closeOrder ()

        let argumentationWanted = shownContext |> Option.exists ArgumentationPolicy.wanted

        // Ok completes the dialog and Reset discards the changes; the action bar places both. Reset is
        // offered only on the workbench: it solves the order again from the rules, which in the plan could
        // change what the plan context policy keeps fixed. Reset also clears the argumentation, including a
        // text not yet sent.
        let reset () =
            setArgumentation ""
            ResetOrderScenario |> dispatch

        let resetAction =
            {|
                label = Terms.Reset |> getTerm "Reset"
                kind = Components.ActionBar.Kind.Secondary
                onClick = reset
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
                                onClick = close
                                disabled = props.busy
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
                Terms.``Prescribe Administration`` |> getTerm "Administration" |> heading
            else
                null

        let content =
            let createStepper = ViewHelpers.createStepper dispatch

            let contentSx =
                {|
                    paddingX = (if isMobile then 1.5 else 2)
                    paddingY = (if isMobile then 1 else 2)
                |}

            let componentSelect =
                match shownOrder with
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
                match shownOrder with
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

            // a select label followed by the unit of its values, as in "dose (mg)"
            let withUnit unit label = $"%s{label} (%s{unit})"

            let substDoseQtySelect =
                match substIndx, shownOrder with
                | Some i, Some ord when ord.Schedule.IsContinuous |> not && itms |> Array.length > 0 ->
                    let label, vals =
                        itms[i].Dose.Quantity.Variable.Vals
                        |> Option.map (fun v ->
                            (Terms.``Order Dose`` |> getTerm "dose" |> withUnit v.Unit),
                            v.Value
                            |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                            |> Array.distinctBy snd
                        )
                        |> Option.defaultValue ("", [||])

                    let severity = itms[i].Dose.Quantity |> markOf

                    vals
                    |> selectFor
                        "substDoseQty"
                        (itemTarget i ItemProperty.DoseQuantity)
                        label
                        None
                        (ChangeSubstanceDoseQuantity >> dispatch)
                        ViewHelpers.noSteps
                        severity
                        None
                | _ -> null

            let substDoseQtyAdjSelect =
                match substIndx, shownOrder with
                | Some i, Some ord when
                    (ord.Schedule.IsOnce || ord.Schedule.IsOnceTimed)
                    && itms |> Array.length > 0
                    && useAdjust
                    ->
                    let label, vals =
                        itms[i].Dose.QuantityAdjust.Variable.Vals
                        |> Option.map (fun v ->
                            (Terms.``Order Dose`` |> getTerm "dose" |> withUnit v.Unit),
                            v.Value
                            |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                            |> Array.distinctBy snd
                        )
                        |> Option.defaultValue ("", [||])

                    let severity = itms[i].Dose.QuantityAdjust |> markOf

                    vals
                    |> selectFor
                        "substDoseQtyAdj"
                        (itemTarget i ItemProperty.DoseQuantityAdjust)
                        label
                        None
                        (ChangeSubstanceDoseQuantityAdjust >> dispatch)
                        ViewHelpers.noSteps
                        severity
                        None
                | _ -> null

            let substPerTimeSelect =
                match substIndx, shownOrder with
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
                             |> getTerm "adjusted dose"
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
                        (itemTarget
                            i
                            (if useAdjust then
                                 ItemProperty.DosePerTimeAdjust
                             else
                                 ItemProperty.DosePerTime))
                        label
                        None
                        dispatch
                        ViewHelpers.noSteps
                        severity
                        None
                | _ -> null

            let substRateSelect =
                let stepper = ViewHelpers.noSteps

                match substIndx, shownOrder with
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
                        (itemTarget
                            i
                            (if useAdjust then
                                 ItemProperty.DoseRateAdjust
                             else
                                 ItemProperty.DoseRate))
                        (Terms.``Order Adjusted dose`` |> getTerm "adjusted dose")
                        None
                        dispatch
                        stepper
                        severity
                        None
                | _ -> null

            let compOrdQtySelect =
                match shownOrder with
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
                        (Models.OrderContext.Target.Component(
                            cmp |> Option.map _.Name |> Option.defaultValue "",
                            ComponentProperty.OrderableQuantity
                        ))
                        (Terms.``Order Preparation Quantity`` |> getTerm "preparation quantity")
                        None
                        (ChangeComponentOrderableQuantity >> dispatch)
                        stepper
                        severity
                        None
                | _ -> null

            let substCompConcSelect =
                match substIndx, shownOrder with
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
                                (Models.OrderContext.Target.Item(cname, iname, ItemProperty.ComponentConcentration))
                                productStrength
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
                                        (Models.OrderContext.Target.Item(
                                            cmp.Name,
                                            itm.Name,
                                            ItemProperty.ComponentConcentration
                                        ))
                                        productStrength
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
                match substIndx, shownOrder with
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
                        (itemTarget i ItemProperty.OrderableQuantity)
                        $"{itms[i].Name} hoeveelheid"
                        None
                        (ChangeSubstanceOrderableQuantity >> dispatch)
                        ViewHelpers.noSteps
                        severity
                        None
                | _ -> null

            let substOrdConcSelect =
                match substIndx, shownOrder with
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
                        (itemTarget i ItemProperty.OrderableConcentration)
                        $"{itms[i].Name} concentratie"
                        None
                        (ChangeSubstanceOrderableConcentration >> dispatch)
                        ViewHelpers.noSteps
                        severity
                        None
                | _ -> null

            let ordQtySelect =
                match shownOrder with
                | Some ord when ord.Orderable.Components |> Array.length > 1 ->
                    let severity = ord.Orderable.OrderableQuantity |> markOf

                    ord.Orderable.OrderableQuantity
                    |> ViewHelpers.ovarVals string
                    |> selectFor
                        "ordQty"
                        (Models.OrderContext.Target.Orderable OrderableProperty.Quantity)
                        (Terms.``Order Total Quantity`` |> getTerm "total quantity")
                        None
                        (ChangeOrderableQuantity >> dispatch)
                        ViewHelpers.noSteps
                        severity
                        None
                | _ -> null

            let frequencySelect =
                match shownOrder with
                | Some ord when ord.Schedule.IsDiscontinuous || ord.Schedule.IsTimed ->
                    let xs = ord.Schedule.Frequency |> ViewHelpers.ovarValsWithRange string 3

                    let stepper =
                        let mode =
                            ord.Schedule.Frequency
                            |> QuantityModePolicy.decideFor QuantityModePolicy.Field.Frequency ord

                        ViewHelpers.frequencyStepper
                            dispatch
                            mode
                            SetMinFrequencyProperty
                            DecreaseFrequencyProperty
                            SetMedianFrequencyProperty
                            IncreaseFrequencyProperty
                            SetMaxFrequencyProperty

                    let severity = ord.Schedule.Frequency |> markOf

                    selectFor
                        "frequency"
                        (Models.OrderContext.Target.Schedule ScheduleProperty.Frequency)
                        (Terms.``Order Frequency`` |> getTerm "frequency")
                        None
                        (ChangeFrequency >> dispatch)
                        stepper
                        severity
                        None
                        xs
                | _ -> null

            let ordDoseQtySelect =
                match shownOrder with
                | Some ord when ord.Schedule.IsContinuous |> not ->
                    let stepper =
                        ViewHelpers.createDoseQtyStepper
                            dispatch
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
                        (Models.OrderContext.Target.Orderable OrderableProperty.DoseQuantity)
                        (Terms.``Order Dose Quantity`` |> getTerm "dose quantity")
                        None
                        (ChangeOrderableDoseQuantity >> dispatch)
                        stepper
                        severity
                        None
                | _ -> null

            // The share of the full intake that a TPN gives, set as on the nutrition page.
            let intakeSlider =
                match shownContext, shownOrder with
                | Some ctx, Some ord when ctx.Category = OrderCategory.Nutrition NutritionCategory.TPN ->
                    Components.IntakeSlider.View
                        {|
                            labelId = $"order-intake-label-%s{ctx.Id}"
                            share = ord |> IntakePolicy.doseShare
                            disabled = not (PlanContextPolicy.setsDoseShare props.editing) || props.busy
                            onCommit = SetDoseQuantityPercProperty >> dispatch
                            order = box ord
                            texts = ViewHelpers.intakeSliderTexts getTerm
                        |}
                | _ -> null

            let ordDoseRateSelect =
                match shownOrder with
                | Some ord when ord.Schedule.IsContinuous || ord.Schedule.IsTimed || ord.Schedule.IsOnceTimed ->
                    let stepper =
                        let mode =
                            ord.Orderable.Dose.Rate
                            |> QuantityModePolicy.decideFor QuantityModePolicy.Field.DoseRate ord

                        ViewHelpers.doseRateStepper
                            dispatch
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
                        (Models.OrderContext.Target.Orderable OrderableProperty.DoseRate)
                        (Terms.``Order Drip rate`` |> getTerm "drip rate")
                        None
                        (ChangeOrderableDoseRate >> dispatch)
                        stepper
                        severity
                        None
                | _ -> null

            let timeSelect =
                match shownOrder with
                | Some ord ->
                    let severity = ord.Schedule.Time |> markOf

                    ord.Schedule.Time
                    |> ViewHelpers.ovarValsWithRange (fixPrecision 2) 2
                    |> Array.distinctBy snd
                    |> selectFor
                        "time"
                        (Models.OrderContext.Target.Schedule ScheduleProperty.Time)
                        (Terms.``Order Administration time`` |> getTerm "administration time")
                        None
                        (ChangeTime >> dispatch)
                        ViewHelpers.noSteps
                        severity
                        None
                | _ -> null

            // The preparation section: a heading like the other sections, with a button beside the name
            // that opens or folds it, and its five fields below while it is open.
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

                    let label = Terms.``Prescribe Preparation`` |> getTerm "Preparation"

                    // The button gets the section name as its label, because a screen reader cannot read the
                    // icon and the name beside it is not linked to the button.
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

            // The argumentation field, last: shown when the rules mark the order or a text is present, and
            // limited to the maximum length of the policy.
            let argumentationField =
                if not argumentationWanted then
                    null
                else
                    let label = Terms.``Order Argumentation`` |> getTerm "Argumentation"

                    let helper =
                        Terms.``Order Argumentation Helper``
                        |> getTerm "why the dose differs from what the rules allow"

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
                        disabled={isOrderLoading}
                        slotProps={inputProps}
                    />
                    """

            // The fields of the dialog in the order they are shown, hard-coded for every dose type. Each field
            // renders nothing when it does not apply to the order. No field is marked as the first one for the
            // user; the server is meant to send that and the order of the fields with the order.
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
                    intakeSlider
                    ordDoseRateSelect
                    timeSelect
                    argumentationField
                |]
                |> unbox<seq<ReactElement>>
                |> React.Fragment

            // The spinner lies over the fields and takes no room, so the dialog keeps its height and the
            // fields stay in place while the order reloads.
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

            let orderTitle =
                shownOrder
                |> Option.map orderName
                |> Option.defaultValue (Terms.``Order Loading`` |> getTerm "order is loading ...")

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
                title={orderTitle}
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

        let card =
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

        // The dialog has its own frame and close; the page mounts it while an order is selected.
        Components.DialogShell.View
            {|
                isOpen = true
                onClose = close
                maxWidth = 500
                children = card
            |}
