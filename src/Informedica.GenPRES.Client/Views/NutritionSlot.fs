namespace Views


module Trail = Informedica.GenPRES.Client.Core.Trail

#nowarn "1104"

/// The logic of one nutrition slot, shared by the enteral and the parenteral layout: the slot
/// messages, their trail entries, and a hook that builds the controls from the order context of
/// the slot.
module NutritionSlot =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines

    open Fable.Core
    open Fable.Core.JsInterop
    open Fable.React
    open Feliz
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Models.Order
    open Elmish
    open Utils
    open FSharp.Core
    open OrderPlanMachine


    /// Whether a context is a nutrition order of one of the categories.
    let isOneOf (categories: NutritionCategory list) (ctx: OrderContext) =
        match OrderContext.nutritionCategory ctx with
        | Some category -> categories |> List.contains category
        | None -> false


    let enteral = [ NutritionCategory.EnteralFeeding; NutritionCategory.EnteralSupplement ]


    let parenteral =
        [
            NutritionCategory.TPN
            NutritionCategory.Lipid
            NutritionCategory.ElectrolyteGlucose
        ]


    /// Returns the order context a nutrition slot shows; None without patient data.
    let contextOf (slot: Informedica.GenPRES.Client.Core.Client.SlotView) =
        slot.Context |> OrderContextMachine.OrderContextView.context


    /// Returns the prescribe action of a nutrition slot, or None. The action exists only when the slot
    /// names the order it adds, no change to the plan is under way and the page is not busy, because the
    /// plan takes one change at a time.
    let prescribeOf
        (nutrition: AppEnv.INutrition)
        planChanging
        held
        (slot: Informedica.GenPRES.Client.Core.Client.SlotView)
        =
        match slot.PrescribeOrderId with
        | Some orderId when not planChanging && not held -> Some(fun () -> nutrition.Prescribe(slot.Id, orderId))
        | Some _
        | None -> None


    /// Shows the bar that ends a slot: reset on the left, prescribe on the right. The prescribe button is
    /// disabled while the slot cannot be prescribed.
    let prescribeBar getTerm (resetAction: Components.ActionBar.Action) (onPrescribe: (unit -> unit) option) =
        let prescribe =
            {|
                label = Terms.``Prescribe`` |> getTerm "Prescribe"
                kind = Components.ActionBar.Kind.Primary
                onClick = onPrescribe |> Option.defaultValue ignore
                disabled = onPrescribe.IsNone
                icon = Some Mui.Icons.Add
            |}

        Components.ActionBar.View {| actions = [| resetAction; prescribe |] |}


    module private Elmish =


        /// The local state of the slot: the selected component. The order is not stored here; it
        /// comes from the order context of the slot.
        type State = { SelectedComponent: string option }

        type Msg =
            | ChangeComponent of string option
            | ChangeComponentOrderableQuantity of cmp: string * string option
            | ChangeComponentDoseQuantityAdjust of cmp: string * string option
            | ChangeOrderableDoseRate of string option
            | ChangeOrderableQuantity of string option
            | ChangeFrequency of string option
            | UpdateOrderScenario of OrderContext.Target option * string option
            | ResetOrderScenario
            // Rate steps
            | DecreaseDoseRateProperty of ntimes: int * useCalc: bool
            | IncreaseDoseRateProperty of ntimes: int * useCalc: bool
            | SetMinDoseRateProperty
            | SetMaxDoseRateProperty
            | SetMedianDoseRateProperty
            // Dose Quantity steps
            | ChangeOrderableDoseQuantity of string option
            | DecreaseDoseQuantityProperty of ntimes: int * useCalc: bool
            | IncreaseDoseQuantityProperty of ntimes: int * useCalc: bool
            | SetMinDoseQuantityProperty
            | SetMaxDoseQuantityProperty
            | SetMedianDoseQuantityProperty
            | SetDoseQuantityPercProperty of perc: int
            // Sets the dose to 100% of the total; the slot sends it by itself once the TPN is composed.
            | StartDoseQuantityPercProperty
            // Component Quantity steps (carries component name)
            | DecreaseComponentQuantityProperty of cmp: string * ntimes: int * useCalc: bool
            | IncreaseComponentQuantityProperty of cmp: string * ntimes: int * useCalc: bool
            | SetMinComponentQuantityProperty of cmp: string
            | SetMaxComponentQuantityProperty of cmp: string
            | SetMedianComponentQuantityProperty of cmp: string
            // Frequency steps
            | DecreaseFrequencyProperty
            | IncreaseFrequencyProperty
            | SetMinFrequencyProperty
            | SetMaxFrequencyProperty
            | SetMedianFrequencyProperty


        /// Creates the state with the selected component from the single scenario of the order
        /// context: the component of the previous render for the same order, else the component of
        /// the scenario, else the first component. Logs an error when there is more than one scenario.
        let init (kept: DialogTabPolicy.Tab option) (ctx: OrderContext) =
            if ctx.Scenarios |> Array.length > 1 then
                Logging.error "received multiple scenarios" ctx.Scenarios.Length

            let cmp =
                ctx.Scenarios
                |> Array.tryExactlyOne
                |> Option.bind (DialogTabPolicy.tab kept >> _.Component)

            { SelectedComponent = cmp }, Cmd.none


        let update
            updateOrderScenario
            resetOrderScenario
            (stepper:
                {|
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

                    setFreqMin: OrderLoader -> unit
                    setFreqDec: OrderLoader -> unit
                    setFreqMed: OrderLoader -> unit
                    setFreqInc: OrderLoader -> unit
                    setFreqMax: OrderLoader -> unit
                |})
            (shown: Order option)
            (msg: Msg)
            (state: State)
            : State * Cmd<Msg>
            =
            // Every change and step acts on the order shown, which comes from the order context of
            // the slot. The plan lane holds that order; the slot state does not.
            let handleStep step =
                match shown with
                | None -> state, Cmd.none
                | Some ord ->
                    OrderLoader.create state.SelectedComponent None ord |> step
                    state, Cmd.none

            let handleStepWithCmp cmpName step =
                match shown with
                | None -> state, Cmd.none
                | Some ord ->
                    OrderLoader.create (Some cmpName) None ord |> step
                    state, Cmd.none

            // Sends a selected value of a variable of the order shown as an update; does nothing
            // without an order.
            let pick target s =
                match shown with
                | Some _ -> state, Cmd.ofMsg (UpdateOrderScenario(Some target, s))
                | None -> state, Cmd.none

            match msg with

            | UpdateOrderScenario(target, s) ->
                match shown with
                | Some ord -> updateOrderScenario (OrderLoader.create state.SelectedComponent None ord) target s
                | None -> ()

                state, Cmd.none

            | ResetOrderScenario ->
                match shown with
                | Some ord -> OrderLoader.create state.SelectedComponent None ord |> resetOrderScenario
                | None -> ()

                state, Cmd.none

            | ChangeComponent cmp ->
                match cmp with
                | None -> state, Cmd.none
                | Some _ -> { state with SelectedComponent = cmp }, Cmd.none

            | ChangeComponentOrderableQuantity(cmpName, s) ->
                pick (OrderContext.Target.Component(cmpName, ComponentProperty.OrderableQuantity)) s
            | ChangeComponentDoseQuantityAdjust(cmpName, s) ->
                pick (OrderContext.Target.Component(cmpName, ComponentProperty.DoseQuantityAdjust)) s
            | ChangeOrderableDoseRate s -> pick (OrderContext.Target.Orderable OrderableProperty.DoseRate) s
            | ChangeOrderableQuantity s -> pick (OrderContext.Target.Orderable OrderableProperty.Quantity) s
            | ChangeFrequency s -> pick (OrderContext.Target.Schedule ScheduleProperty.Frequency) s
            | ChangeOrderableDoseQuantity s -> pick (OrderContext.Target.Orderable OrderableProperty.DoseQuantity) s

            // Rate steps
            | SetMinDoseRateProperty -> handleStep stepper.setRateMin
            | DecreaseDoseRateProperty(n, uc) -> handleStep (stepper.setRateDec (n, uc))
            | SetMedianDoseRateProperty -> handleStep stepper.setRateMed
            | IncreaseDoseRateProperty(n, uc) -> handleStep (stepper.setRateInc (n, uc))
            | SetMaxDoseRateProperty -> handleStep stepper.setRateMax

            // Dose Quantity steps
            | SetMinDoseQuantityProperty -> handleStep stepper.setDoseQtyMin
            | DecreaseDoseQuantityProperty(n, uc) -> handleStep (stepper.setDoseQtyDec (n, uc))
            | SetMedianDoseQuantityProperty -> handleStep stepper.setDoseQtyMed
            | IncreaseDoseQuantityProperty(n, uc) -> handleStep (stepper.setDoseQtyInc (n, uc))
            | SetMaxDoseQuantityProperty -> handleStep stepper.setDoseQtyMax
            | SetDoseQuantityPercProperty perc -> handleStep (stepper.setDoseQtyPerc perc)
            | StartDoseQuantityPercProperty -> handleStep (stepper.setDoseQtyPerc 100)

            // Component Quantity steps
            | SetMinComponentQuantityProperty cmp -> handleStepWithCmp cmp stepper.setComponentQtyMin
            | DecreaseComponentQuantityProperty(cmp, n, uc) ->
                handleStepWithCmp cmp (stepper.setComponentQtyDec (n, uc))
            | SetMedianComponentQuantityProperty cmp -> handleStepWithCmp cmp stepper.setComponentQtyMed
            | IncreaseComponentQuantityProperty(cmp, n, uc) ->
                handleStepWithCmp cmp (stepper.setComponentQtyInc (n, uc))
            | SetMaxComponentQuantityProperty cmp -> handleStepWithCmp cmp stepper.setComponentQtyMax

            // Frequency steps
            | SetMinFrequencyProperty -> handleStep stepper.setFreqMin
            | DecreaseFrequencyProperty -> handleStep stepper.setFreqDec
            | SetMedianFrequencyProperty -> handleStep stepper.setFreqMed
            | IncreaseFrequencyProperty -> handleStep stepper.setFreqInc
            | SetMaxFrequencyProperty -> handleStep stepper.setFreqMax


    open Elmish


    /// The kind of a message: a local change of the slot, a value change, a step, an update or a reset.
    [<RequireQualifiedAccess>]
    type private Kind =
        | Local
        | Change
        | Step of string
        | Update
        | Reset


    let private kindOf msg =
        match msg with
        | ChangeComponent _ -> Kind.Local
        | UpdateOrderScenario _ -> Kind.Update
        | ResetOrderScenario -> Kind.Reset
        | SetMinDoseRateProperty
        | SetMinDoseQuantityProperty
        | SetMinComponentQuantityProperty _
        | SetMinFrequencyProperty -> Kind.Step "min"
        | DecreaseDoseRateProperty _
        | DecreaseDoseQuantityProperty _
        | DecreaseComponentQuantityProperty _
        | DecreaseFrequencyProperty -> Kind.Step "dec"
        | SetMedianDoseRateProperty
        | SetMedianDoseQuantityProperty
        | SetMedianComponentQuantityProperty _
        | SetMedianFrequencyProperty -> Kind.Step "med"
        | IncreaseDoseRateProperty _
        | IncreaseDoseQuantityProperty _
        | IncreaseComponentQuantityProperty _
        | IncreaseFrequencyProperty -> Kind.Step "inc"
        | SetMaxDoseRateProperty
        | SetMaxDoseQuantityProperty
        | SetMaxComponentQuantityProperty _
        | SetMaxFrequencyProperty -> Kind.Step "max"
        | SetDoseQuantityPercProperty _
        | StartDoseQuantityPercProperty -> Kind.Step "perc"
        | _ -> Kind.Change


    /// Returns the field a message changes, with the component name for a component field; None for a
    /// message without a field.
    let private fieldOf msg =
        match msg with
        | ChangeComponentOrderableQuantity(cmp, _)
        | SetMinComponentQuantityProperty cmp
        | DecreaseComponentQuantityProperty(cmp, _, _)
        | SetMedianComponentQuantityProperty cmp
        | IncreaseComponentQuantityProperty(cmp, _, _)
        | SetMaxComponentQuantityProperty cmp -> Some $"compOrdQty %s{cmp}"
        | ChangeComponentDoseQuantityAdjust(cmp, _) -> Some $"compDoseQtyAdj %s{cmp}"
        | ChangeOrderableDoseRate _
        | SetMinDoseRateProperty
        | DecreaseDoseRateProperty _
        | SetMedianDoseRateProperty
        | IncreaseDoseRateProperty _
        | SetMaxDoseRateProperty -> Some "ordDoseRate"
        | ChangeOrderableDoseQuantity _
        | SetMinDoseQuantityProperty
        | DecreaseDoseQuantityProperty _
        | SetMedianDoseQuantityProperty
        | IncreaseDoseQuantityProperty _
        | SetMaxDoseQuantityProperty
        | SetDoseQuantityPercProperty _
        | StartDoseQuantityPercProperty -> Some "ordDoseQty"
        | ChangeOrderableQuantity _ -> Some "ordQty"
        | ChangeFrequency _
        | SetMinFrequencyProperty
        | DecreaseFrequencyProperty
        | SetMedianFrequencyProperty
        | IncreaseFrequencyProperty
        | SetMaxFrequencyProperty -> Some "frequency"
        | ChangeComponent _
        | UpdateOrderScenario _
        | ResetOrderScenario -> None


    /// Describes a message for the trail: its case, its value and its field.
    let private describeMsg msg =
        let value (s: string option) = s |> Option.defaultValue "none"

        let steps (n: int, useCalc: bool) = if useCalc then $"%i{n} calc" else $"%i{n}"

        let case =
            match msg with
            | ChangeComponent s -> $"ChangeComponent %s{value s}"
            | ChangeComponentOrderableQuantity(_, s) -> $"ChangeComponentOrderableQuantity %s{value s}"
            | ChangeComponentDoseQuantityAdjust(_, s) -> $"ChangeComponentDoseQuantityAdjust %s{value s}"
            | ChangeOrderableDoseRate s -> $"ChangeOrderableDoseRate %s{value s}"
            | ChangeOrderableQuantity s -> $"ChangeOrderableQuantity %s{value s}"
            | ChangeFrequency s -> $"ChangeFrequency %s{value s}"
            | ChangeOrderableDoseQuantity s -> $"ChangeOrderableDoseQuantity %s{value s}"
            | UpdateOrderScenario(_, s) -> $"UpdateOrderScenario %s{value s}"
            | ResetOrderScenario -> "ResetOrderScenario"
            | DecreaseDoseRateProperty(n, uc) -> $"DecreaseDoseRateProperty %s{steps (n, uc)}"
            | IncreaseDoseRateProperty(n, uc) -> $"IncreaseDoseRateProperty %s{steps (n, uc)}"
            | SetMinDoseRateProperty -> "SetMinDoseRateProperty"
            | SetMaxDoseRateProperty -> "SetMaxDoseRateProperty"
            | SetMedianDoseRateProperty -> "SetMedianDoseRateProperty"
            | DecreaseDoseQuantityProperty(n, uc) -> $"DecreaseDoseQuantityProperty %s{steps (n, uc)}"
            | IncreaseDoseQuantityProperty(n, uc) -> $"IncreaseDoseQuantityProperty %s{steps (n, uc)}"
            | SetMinDoseQuantityProperty -> "SetMinDoseQuantityProperty"
            | SetMaxDoseQuantityProperty -> "SetMaxDoseQuantityProperty"
            | SetMedianDoseQuantityProperty -> "SetMedianDoseQuantityProperty"
            | SetDoseQuantityPercProperty perc -> $"SetDoseQuantityPercProperty %i{perc}"
            | StartDoseQuantityPercProperty -> "StartDoseQuantityPercProperty 100 automatic"
            | DecreaseComponentQuantityProperty(_, n, uc) -> $"DecreaseComponentQuantityProperty %s{steps (n, uc)}"
            | IncreaseComponentQuantityProperty(_, n, uc) -> $"IncreaseComponentQuantityProperty %s{steps (n, uc)}"
            | SetMinComponentQuantityProperty _ -> "SetMinComponentQuantityProperty"
            | SetMaxComponentQuantityProperty _ -> "SetMaxComponentQuantityProperty"
            | SetMedianComponentQuantityProperty _ -> "SetMedianComponentQuantityProperty"
            | DecreaseFrequencyProperty -> "DecreaseFrequencyProperty"
            | IncreaseFrequencyProperty -> "IncreaseFrequencyProperty"
            | SetMinFrequencyProperty -> "SetMinFrequencyProperty"
            | SetMaxFrequencyProperty -> "SetMaxFrequencyProperty"
            | SetMedianFrequencyProperty -> "SetMedianFrequencyProperty"

        match fieldOf msg with
        | Some field -> $"%s{case} field %s{field}"
        | None -> case


    /// Describes the calls a message makes for the trail, given the order shown. Without an order shown,
    /// only an update makes a call.
    let private effectsOf (shown: Order option) msg =
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
            let field = fieldOf msg |> Option.defaultValue "?"
            [ $"CallStep %s{field} %s{step}" ]


    /// Describes the local state of the slot for the trail: the selected component. A component step
    /// names its component in the message instead.
    let private describeState (state: State) =
        let cmp = state.SelectedComponent |> Option.defaultValue "none"
        $"selected %s{cmp}"


    let halfSize =
        {|
            xs = 12
            md = 6
        |}

    let cellSx =
        {|
            minWidth = 350
            ``& .MuiFormControl-root`` = {| width = "100%" |}
        |}

    let flexEndSx = {| alignItems = "flex-end" |}

    // The component rows have three columns: name, quantity and range, the last two of equal width.
    // Below the large breakpoint the columns stack, because the quantity column is then narrower than
    // the 400px of the quantity field.
    let cmpLabelSize =
        {|
            xs = 12
            lg = 3
        |}

    let cmpQtySize =
        {|
            xs = 12
            lg = 4.5
        |}

    let cmpRangeSize =
        {|
            xs = 12
            lg = 4.5
        |}

    // A quantity field ends with the room for its severity mark, so the middle column gets the same
    // padding on its left while the columns are side by side.
    let cmpMiddleSx =
        {|
            paddingLeft =
                {|
                    xs = "0px"
                    lg = $"%i{Components.QuantityField.markWidth}px"
                |}
        |}

    // The controls in the right column take its full width, so they have equal widths; a field fits
    // the room for its severity mark within that width.
    let cmpRightSx = {| ``& .MuiFormControl-root`` = {| width = "100%" |} |}

    // the column headings only make sense while the columns stand side by side
    let cmpHeaderSx =
        {|
            display =
                {|
                    xs = "none"
                    lg = "flex"
                |}
        |}

    // The name is in the first column, so the caption of the field is hidden from sight. The caption
    // stays in the page for screen readers, and the field uses it as the id of its select.
    let cmpQtySx =
        {|
            minWidth = 350
            ``& .MuiFormControl-root`` = {| width = "100%" |}
            ``& [data-part="label"]`` =
                {|
                    position = "absolute"
                    width = "1px"
                    height = "1px"
                    overflow = "hidden"
                    clip = "rect(0 0 0 0)"
                    whiteSpace = "nowrap"
                |}
        |}

    // When the columns stack, the headings are hidden, so the range shows its own caption.
    let cmpRangeCaptionSx =
        {|
            display =
                {|
                    xs = "block"
                    lg = "none"
                |}
        |}

    let alignCenterSx = {| alignItems = "center" |}


    // the summary wraps onto more lines rather than clip a value at the header's edge
    let flexOverflowSx =
        {|
            display = "flex"
            flexWrap = "wrap"
            alignItems = "center"
            width = "100%"
        |}


    // Shows the administration of a scenario as pills, one per text block from the server, each with
    // its own severity.
    let adminSummary (name: string) (rows: TextBlock[][]) =
        Components.AdministrationSummary.View
            {|
                name = name
                rows =
                    rows
                    |> Array.map (
                        Array.map (fun block ->
                            {|
                                text = block |> ViewHelpers.textBlockText |> String.trim
                                severity = block |> Severity.ofTextBlock
                            |}
                        )
                    )
            |}


    /// The controls of one slot, built from its order context and ready to be laid out.
    type Slot =
        {
            /// The slot's name, in the user's language.
            Label: string
            /// Whether the window is narrow: filters are scrolled, not typed into.
            IsMobile: bool
            /// The order of the single scenario of the order context; None otherwise.
            Order: Order option
            /// The composition filter.
            GenericFilter: JSX.Element
            /// The indication filter, when there is more than one indication.
            IndicationFilter: JSX.Element
            /// The dose type filter, when there is more than one dose type.
            DoseTypeFilter: JSX.Element
            /// The frequency field, for an order given in doses.
            FrequencyControl: JSX.Element
            /// The field of the quantity given per dose.
            DoseQuantityControl: JSX.Element
            /// The adjusted dose per time, read-only.
            DosePerTimeAdjustDisplay: JSX.Element
            /// The rate and the time side by side, for a timed or continuous order.
            RateControl: JSX.Element
            /// The rate field alone, at the least width of the fields, for a timed or continuous
            /// order.
            RateField: JSX.Element
            /// The run time of the order alone, read-only, at the least width of the fields, for a timed
            /// or continuous order.
            TimeDisplay: JSX.Element
            /// The total volume, read-only.
            TotalVolumeDisplay: JSX.Element
            /// The headings above the component rows.
            ComponentHeader: JSX.Element
            /// A row per component: its name, its quantity and the recommended range.
            ComponentRows: JSX.Element[]
            /// The heading above the administration fields.
            AdministrationHeading: JSX.Element
            /// The action that discards the changes to the order, for a bar of its own.
            ResetAction: Components.ActionBar.Action
            /// The button that discards the changes to the order.
            ResetBar: JSX.Element
            /// Sets the dose quantity to a percentage of the total.
            SetDoseQuantityPerc: int -> unit
            /// Whether the intake slider can set the dose: the TPN is composed and its dose count has
            /// a single value.
            CanSetDoseShare: bool
            /// The dose as a percentage of the total, when both have a single value.
            DoseQuantityShare: decimal option
            /// The notice that explains why the components are locked while the TPN dose is less than
            /// 100% of the total.
            CompositionNotice: JSX.Element
            /// The warning shown when the order is locked in the plan, or when the rules give more than
            /// one scenario so no order can be shown.
            ScenarioNotice: JSX.Element
        }


    /// Builds the controls of a slot from its order context; every change is sent as a command. This
    /// is a React hook: call it at the top of a component on every render.
    let useSlot
        (props:
            {|
                nutritionContext: OrderContext
                // The order plan, used to check whether the order context is locked and whether the
                // intake slider can set the dose.
                plan: OrderPlan
                // Sends a command for the order context of the slot.
                evaluateContext: string * Api.OrderViewCommand -> unit
                // Called when the recalculation list closes without a selection.
                planRestore: unit -> unit
                localizationTerms: Deferred<string[][]>
                // True while a request that changes the page is under way; the slot then sends
                // nothing on its own.
                busy: bool
                // the least width of the frequency, dose and dose per time fields
                fieldMinWidth: int option
            |})
        =
        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let getTerm = Global.getLocalizedTerm props.localizationTerms lang
        let recommendedRange = Terms.``Nutrition Recommended Range`` |> getTerm "Recommended range"

        let ctx = props.nutritionContext
        let ncId = props.nutritionContext.Id

        let label =
            let name = OrderContextText.label ctx

            match OrderContext.nutritionCategory ctx with
            | Some NutritionCategory.EnteralFeeding -> Terms.``Nutrition Enteral Feeding`` |> getTerm name
            | Some NutritionCategory.EnteralSupplement -> Terms.``Nutrition Enteral Supplement`` |> getTerm name
            | Some NutritionCategory.TPN -> Terms.``Nutrition TPN`` |> getTerm name
            | Some NutritionCategory.Lipid -> Terms.``Nutrition Lipids`` |> getTerm name
            | Some NutritionCategory.ElectrolyteGlucose -> Terms.``Nutrition Electrolytes Glucose`` |> getTerm name
            | None -> name

        let isMobile = Mui.Hooks.useIsMobile ()

        let fixPrecision = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision

        let markOf = ViewHelpers.markOf

        let evaluate cmd = props.evaluateContext (ncId, cmd)

        // Sends a filter selection as its index in the options of the field, and an emptied field as
        // a clear. A value the field does not offer is a bug: it is logged and not sent.
        let pickFilter field (options: 'a[]) (picked: 'a option) =
            PickPolicy.filterCommand field options picked
            |> ViewHelpers.sendPick evaluate picked

        let genericChange s = pickFilter OrderContext.Generic ctx.Filter.Generics s

        let indicationChange s = pickFilter OrderContext.Indication ctx.Filter.Indications s

        let doseTypeChange s =
            let dt = s |> Option.map DoseTypeText.doseTypeFromString
            pickFilter OrderContext.DoseType ctx.Filter.DoseTypes dt

        // Sends a selected value as its index in the values of the order variable. A value the order
        // does not offer, or a change without a matching field, is logged as a warning and not sent.
        let updateOrderScenario (ol: OrderLoader) target (s: string option) =
            let located =
                target
                |> Option.bind (fun t -> ol.Order |> OrderContext.Target.tryGet t |> Option.map (fun v -> t, v))

            match located, s with
            | Some(t, ovar), Some key ->
                match OrderContext.values ovar |> Array.tryFindIndex ((=) key) with
                | Some n -> evaluate (ViewHelpers.setNthCommand t n)
                | None -> Logging.warning "a value the field does not offer is not sent" key
            | _ -> Logging.warning "a change no field holds is not sent" s

        let resetOrderScenario (_ol: OrderLoader) = evaluate Api.OrderViewCommand.ResetOrderScenario

        let stepper =
            let create cmd = fun (_: OrderLoader) -> evaluate cmd

            let createWithN cmd = fun (n, uc) (_: OrderLoader) -> evaluate (cmd (n, uc))

            let createWithCmp cmd =
                fun (ol: OrderLoader) -> ol.Component |> Option.iter (cmd >> evaluate)

            let createWithCmpN cmd =
                fun (n, uc) (ol: OrderLoader) -> ol.Component |> Option.iter (fun c -> evaluate (cmd (c, n, uc)))

            {|
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
                // Frequency
                setFreqMin = create Api.OrderViewCommand.SetMinScheduleFrequencyProperty
                setFreqDec = create Api.OrderViewCommand.DecreaseScheduleFrequencyProperty
                setFreqMed = create Api.OrderViewCommand.SetMedianScheduleFrequencyProperty
                setFreqInc = create Api.OrderViewCommand.IncreaseScheduleFrequencyProperty
                setFreqMax = create Api.OrderViewCommand.SetMaxScheduleFrequencyProperty
            |}

        // The order of the single scenario of the order context; None otherwise.
        let shownOrder = ctx.Scenarios |> Array.tryExactlyOne |> Option.map _.Order

        let updateTraced msg state =
            let next, cmd = update updateOrderScenario resetOrderScenario stepper shownOrder msg state

            StepTrail.record (fun no at ->
                Trail.step "Nutrition" describeMsg id describeState no at msg (next, effectsOf shownOrder msg)
            )

            next, cmd

        // The selected component of the last render, so a server answer for the same order keeps
        // the selection.
        let kept = React.useRef<DialogTabPolicy.Tab option> None

        let state, dispatch = React.useElmish (init kept.current ctx, updateTraced, [| box ctx |])

        kept.current <-
            shownOrder
            |> Option.map (fun ord ->
                {
                    OrderId = ord.Id
                    Component = state.SelectedComponent
                    Item = None
                }
                : DialogTabPolicy.Tab
            )

        // A locked order takes no input: its fields wait as during a request, and so does the intake
        // effect, which sends without a click.
        let isLocked = PlanContextPolicy.locked props.plan ctx
        let fieldsWait = props.busy || isLocked

        // The page stays enabled while a field counts its step clicks, because the counting field is
        // on the page; the controls that send a request wait for the count. The fields use fieldsWait,
        // without the count, because the counting field must see its own request go out and come back.
        let counting = React.useContext Global.counting
        let held = fieldsWait || counting.Counting

        let texts = ViewHelpers.quantityFieldTexts getTerm
        let select = ViewHelpers.orderSelect texts true fieldsWait

        let isTpn = ctx |> isOneOf [ NutritionCategory.TPN ]

        // The order and composition for which the dose was last set to 100% of the total.
        let sentFor = React.useRef (None: (string * decimal option[]) option)

        // Once the TPN is composed, its dose is set to 100% of the total, so the dose count has a
        // single value and the intake slider can take over.
        let startIntake () =
            match shownOrder with
            | Some ord when isTpn && not held ->
                match ord |> IntakePolicy.start sentFor.current with
                | IntakePolicy.Start.Clear -> sentFor.current <- None
                | IntakePolicy.Start.Wait -> ()
                | IntakePolicy.Start.Send key ->
                    sentFor.current <- Some key
                    // Runs the update directly: a dispatch from this effect reaches the program that
                    // useElmish is replacing.
                    updateTraced StartDoseQuantityPercProperty state |> ignore
            | _ -> ()

        React.useEffect (startIntake, [| box ctx; box held |])

        // While the TPN dose is less than 100% of the total, the composition is locked: a component
        // step would keep the rate of the partial dose for the whole.
        let isCompositionLocked = isTpn && shownOrder |> Option.exists IntakePolicy.isCompositionLocked

        // the notice of the locked composition may be dismissed; it comes back when the composition
        // is unlocked and then locked again
        let lockNoticeDismissed, setLockNoticeDismissed = React.useState false

        React.useEffect ((fun () -> setLockNoticeDismissed false), [| box isCompositionLocked |])

        let componentSelect = ViewHelpers.orderSelect texts true (fieldsWait || isCompositionLocked)

        // Builds the recalculation of a field; it is allowed when the intent policy allows it for the
        // order shown.
        let reCalcOf target : ViewHelpers.ReCalc =
            {|
                allowed = shownOrder |> Option.exists (IntentPolicy.isReCalcAllowed target)
                reCalc = fun () -> evaluate (ViewHelpers.reCalcCommand target)
                restore = props.planRestore
                busy = fieldsWait
            |}
        // A read-only value has no clear button.
        let display = ViewHelpers.orderFixed texts true fieldsWait
        // The filters of a locked order show the selection and take no input. A selection is sent at
        // once, so the filters also wait during a request and while a field counts its step clicks,
        // because a request under way drops the command that follows it.
        let filterSelect = ViewHelpers.filterSelectWhen (not held)
        let responsiveFilter = ViewHelpers.responsiveFilterWhen (not held) isMobile

        let displayOrder = shownOrder

        // The checkboxes and the diluent choice send at once, so they wait as the filters do, and also
        // while the composition is locked.
        let compositionWaits = held || isCompositionLocked

        let diluentChange s =
            PickPolicy.diluentCommand ctx.Filter.Diluents s
            |> ViewHelpers.sendPick evaluate s

        let componentRows =
            match displayOrder with
            | Some ord ->
                let cmps = ord.Orderable.Components
                let diluent = ctx.Scenarios |> Array.tryExactlyOne |> Option.bind _.Diluent

                let rows =
                    CompositionPolicy.rows
                        ctx.Filter.DoseRuleComponents
                        (cmps |> Array.map _.Name)
                        ctx.Filter.Components
                        diluent
                        ctx.Filter.Diluents

                let componentName color (name: string) =
                    JSX.jsx
                        $"""
                    import Typography from '@mui/material/Typography';
                    <Typography variant="body1" color={color}>{name}</Typography>
                    """

                let checkbox name inOrder =
                    let toggle _ =
                        CompositionPolicy.toggle ctx.Filter.Components rows name |> evaluate

                    let color = if inOrder then "text.primary" else "text.disabled"

                    JSX.jsx
                        $"""
                    import Stack from '@mui/material/Stack';
                    import Checkbox from '@mui/material/Checkbox';
                    <Stack direction="row" sx={alignCenterSx}>
                        <Checkbox checked={inOrder} disabled={compositionWaits} onChange={toggle} />
                        {componentName color name}
                    </Stack>
                    """

                let diluentSelect name choices =
                    Components.PickField.View
                        {|
                            label = Terms.Diluent |> getTerm "Diluent"
                            options = choices |> Array.map (fun s -> s, s)
                            selected = Some name
                            onChange = diluentChange
                            clearable = false
                            isLoading = false
                            enabled = not compositionWaits
                            shape = Components.PickField.Shape.Scroll
                            placeholder = None
                        |}

                // A row for a component of the order: the label cell, the quantity field and the range.
                let componentRow (cmp: Component) labelCell =
                    let qtyVals = cmp.OrderableQuantity |> ViewHelpers.ovarValsWithRange string 3

                    let nav =
                        let cmpName = cmp.Name

                        let mode =
                            cmp.OrderableQuantity
                            |> QuantityModePolicy.decideFor QuantityModePolicy.Field.ComponentQuantity ord

                        ViewHelpers.createStepper
                            dispatch
                            mode
                            (cmp.OrderableQuantity |> ViewHelpers.hasLargeStep)
                            (SetMinComponentQuantityProperty cmpName)
                            (fun (n, uc) -> DecreaseComponentQuantityProperty(cmpName, n, uc))
                            (SetMedianComponentQuantityProperty cmpName)
                            (fun (n, uc) -> IncreaseComponentQuantityProperty(cmpName, n, uc))
                            (SetMaxComponentQuantityProperty cmpName)
                            (cmp.OrderableQuantity |> ViewHelpers.ovarStep string)
                            (cmp.OrderableQuantity |> ViewHelpers.largeStepText)

                    let qtyWarning = cmp.OrderableQuantity |> markOf

                    // The label is hidden from sight but stays in the page, because the field uses it
                    // as the id of its select.
                    let qtyLabel = cmp.OrderableQuantity |> ViewHelpers.ovarLabel cmp.Name

                    let qtyControl =
                        componentSelect
                            false
                            qtyLabel
                            None
                            (fun s -> ChangeComponentOrderableQuantity(cmp.Name, s) |> dispatch)
                            nav
                            qtyWarning
                            (reCalcOf (OrderContext.Target.Component(cmp.Name, ComponentProperty.OrderableQuantity)))
                            (Some 400)
                            qtyVals

                    // the range the solver calculated for the component's quantity, from the rules
                    // that bound it
                    let range, rangeCaption =
                        match
                            cmp.OrderableQuantity.CalculatedConstraints
                            |> OrderDisplay.Variable.renderValue 3
                        with
                        | "" -> "", ""
                        | r -> $"(%s{r})", recommendedRange

                    JSX.jsx
                        $"""
                    import Grid from '@mui/material/Grid';
                    import Box from '@mui/material/Box';
                    import Typography from '@mui/material/Typography';
                    <Grid container spacing={{2}} sx={alignCenterSx}>
                        <Grid size={cmpLabelSize}>
                            {labelCell}
                        </Grid>
                        <Grid size={cmpQtySize} sx={cmpMiddleSx}>
                            <Box sx={cmpQtySx}>
                                {qtyControl}
                            </Box>
                        </Grid>
                        <Grid size={cmpRangeSize}>
                            <Typography variant="caption" color="text.secondary" sx={cmpRangeCaptionSx}>{rangeCaption}</Typography>
                            <Typography variant="body1" color="text.primary">{range}</Typography>
                        </Grid>
                    </Grid>
                    """

                // A row for an additive that is not in the order: only its checkbox and its name.
                let leftOutRow labelCell =
                    JSX.jsx
                        $"""
                    import Grid from '@mui/material/Grid';
                    <Grid container spacing={{2}} sx={alignCenterSx}>
                        <Grid size={cmpLabelSize}>
                            {labelCell}
                        </Grid>
                    </Grid>
                    """

                let componentOf name = cmps |> Array.tryFind (fun c -> c.Name = name)

                rows
                |> Array.choose (
                    function
                    | CompositionPolicy.Row.Fixed name
                    | CompositionPolicy.Row.Diluent(name, [||]) ->
                        componentOf name
                        |> Option.map (fun cmp -> componentRow cmp (componentName "text.primary" name))
                    | CompositionPolicy.Row.Additive(name, true) ->
                        componentOf name
                        |> Option.map (fun cmp -> componentRow cmp (checkbox name true))
                    | CompositionPolicy.Row.Additive(name, false) -> Some(leftOutRow (checkbox name false))
                    | CompositionPolicy.Row.Diluent(name, choices) ->
                        componentOf name
                        |> Option.map (fun cmp -> componentRow cmp (diluentSelect name choices))
                )
            | None -> [| null |]

        let selectMinWidth = props.fieldMinWidth

        let doseQtyControl =
            match displayOrder with
            | Some ord ->
                let severity = ord.Orderable.Dose.Quantity |> markOf

                let label =
                    ord.Orderable.Dose.Quantity
                    |> ViewHelpers.ovarLabel (Terms.``Order Dose Quantity`` |> getTerm "dose quantity")

                let vals = ord.Orderable.Dose.Quantity |> ViewHelpers.ovarValsWithRange string 3

                let doseQtyNav =
                    ViewHelpers.createDoseQtyStepper
                        dispatch
                        ord
                        SetMinDoseQuantityProperty
                        DecreaseDoseQuantityProperty
                        SetMedianDoseQuantityProperty
                        IncreaseDoseQuantityProperty
                        SetMaxDoseQuantityProperty

                select
                    false
                    label
                    None
                    (ChangeOrderableDoseQuantity >> dispatch)
                    doseQtyNav
                    severity
                    (reCalcOf (OrderContext.Target.Orderable OrderableProperty.DoseQuantity))
                    selectMinWidth
                    vals
            | None -> null

        let dosePerTimeAdjDisplay =
            match displayOrder with
            | Some ord ->
                ord.Orderable.Dose.PerTimeAdjust
                |> ViewHelpers.ovarDisplay display "dosering" (fixPrecision 3) selectMinWidth
            | None -> null

        let frequencyControl =
            match displayOrder with
            | Some ord when ord.Schedule.IsDiscontinuous || ord.Schedule.IsTimed ->
                let severity = ord.Schedule.Frequency |> markOf
                let label = ord.Schedule.Frequency |> ViewHelpers.ovarLabel "frequentie"
                let freqVals = ord.Schedule.Frequency |> ViewHelpers.ovarValsWithRange string 3

                let freqNav =
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

                select
                    false
                    label
                    None
                    (ChangeFrequency >> dispatch)
                    freqNav
                    severity
                    (reCalcOf (OrderContext.Target.Schedule ScheduleProperty.Frequency))
                    selectMinWidth
                    freqVals
            | _ -> null

        let genericFilter =
            let sel = ctx.Filter.Generic
            let items = ctx.Filter.Generics
            let lbl = Terms.Composition |> getTerm "Composition"

            items |> responsiveFilter lbl sel genericChange

        let isTimedOrContinuous (ord: Order) = ord.Schedule.IsTimed || ord.Schedule.IsContinuous

        // the rate field, for a timed or continuous order
        let rateField minWidth =
            match displayOrder with
            | Some ord when ord |> isTimedOrContinuous ->
                let nav =
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
                let label = ord.Orderable.Dose.Rate |> ViewHelpers.ovarLabel "infuussnelheid"

                ord.Orderable.Dose.Rate
                |> ViewHelpers.ovarValsWithRange string 3
                |> select
                    false
                    label
                    None
                    (ChangeOrderableDoseRate >> dispatch)
                    nav
                    severity
                    (reCalcOf (OrderContext.Target.Orderable OrderableProperty.DoseRate))
                    minWidth
            | _ -> null

        // The run time of the order, read-only, for a timed or continuous order.
        let timeDisplay minWidth =
            match displayOrder with
            | Some ord when ord |> isTimedOrContinuous ->
                ord.Schedule.Time
                |> ViewHelpers.ovarDisplay display "looptijd" (fixPrecision 3) minWidth
            | _ -> null

        let rateControl =
            match displayOrder with
            | Some ord when ord |> isTimedOrContinuous ->
                let rateDisplay = rateField (Some 400)
                let timeDisplay = timeDisplay (Some 400)

                JSX.jsx
                    $"""
                import Grid from '@mui/material/Grid';
                import Box from '@mui/material/Box';
                <Grid container spacing={{2}} sx={flexEndSx}>
                    <Grid size={halfSize}>
                        <Box sx={cellSx}>
                            {rateDisplay}
                        </Box>
                    </Grid>
                    <Grid size={halfSize}>
                        <Box sx={cellSx}>
                            {timeDisplay}
                        </Box>
                    </Grid>
                </Grid>
                """
            | _ -> null

        // like the component quantities, an unsolved total shows the range it can still take
        let totalVolumeDisplay =
            match displayOrder with
            | Some ord ->
                let ovar = ord.Orderable.OrderableQuantity

                let totalVolumeLabel =
                    ovar
                    |> ViewHelpers.ovarLabel (Terms.``Order Total Volume`` |> getTerm "total volume")

                ovar
                |> ViewHelpers.ovarValsWithRange string 3
                |> display false totalVolumeLabel None ignore ViewHelpers.noSteps (ovar |> markOf) (Some 400)
            | None -> null

        let onClickReset = fun () -> ResetOrderScenario |> dispatch

        let heading label =
            Components.SectionHeading.View
                {|
                    label = label
                    action = None
                |}

        let administrationDivider = Terms.``Prescribe Administration`` |> getTerm "Administration" |> heading

        let componentHeading = Terms.``Nutrition Component`` |> getTerm "Component" |> heading
        let quantityHeading = Terms.``Nutrition Quantity`` |> getTerm "Quantity" |> heading
        let rangeHeading = recommendedRange |> heading

        let headerRow =
            JSX.jsx
                $"""
            import Grid from '@mui/material/Grid';
            <Grid container spacing={{2}} sx={cmpHeaderSx}>
                <Grid size={cmpLabelSize}>
                    {componentHeading}
                </Grid>
                <Grid size={cmpQtySize} sx={cmpMiddleSx}>
                    {quantityHeading}
                </Grid>
                <Grid size={cmpRangeSize}>
                    {rangeHeading}
                </Grid>
            </Grid>
            """

        // The reset discards the changes to the order. The action bar shows it narrow and on the left,
        // so it is not as wide as the panel and not where the user clicks by default.
        let resetAction =
            {|
                label = Terms.Reset |> getTerm "Reset"
                kind = Components.ActionBar.Kind.Secondary
                onClick = onClickReset
                disabled = held
                icon = Some Mui.Icons.RefreshIcon
            |}

        let resetBar = Components.ActionBar.View {| actions = [| resetAction |] |}

        let compositionLockedText =
            Terms.``Nutrition Composition Locked``
            |> getTerm "Set the dose quantity to 100% first to change the composition."

        // padding, because the Stack the notice sits in sets the top margin of its children
        let compositionNoticeSx = {| paddingTop = 1 |}

        let compositionNotice =
            if isCompositionLocked && not lockNoticeDismissed then
                let notice =
                    Components.Notice.View
                        {|
                            kind = Components.Notice.Kind.Info
                            title = None
                            message = compositionLockedText
                            action = None
                            onClose = Some(fun () -> setLockNoticeDismissed true)
                        |}

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                <Box sx={compositionNoticeSx}>{notice}</Box>
                """
            else
                null

        // Warns that the order is locked in the plan, or that the rules give more than one scenario,
        // so no order is shown.
        let scenarioNotice =
            if isLocked then
                Components.Notice.View
                    {|
                        kind = Components.Notice.Kind.Warning
                        title = None
                        message =
                            Terms.``Plan Context Locked``
                            |> getTerm (
                                Informedica.GenPRES.Client.Core.MachinePolicies.SigningPolicy.english
                                    Terms.``Plan Context Locked``
                            )
                        action = None
                        onClose = None
                    |}
            elif ctx.Scenarios |> Array.length > 1 then
                Components.Notice.View
                    {|
                        kind = Components.Notice.Kind.Warning
                        title = None
                        message =
                            Terms.``Nutrition Several Rules``
                            |> getTerm "More than one rule was found for this choice, so no order can be shown"
                        action = None
                        onClose = None
                    |}
            else
                null

        let canSetDoseShare = PlanContextPolicy.canSetDoseShare props.plan ctx

        let indicationFilter =
            if ctx.Filter.Generic.IsNone || ctx.Filter.Indications |> Array.length <= 1 then
                null
            else
                let sel = ctx.Filter.Indication
                let items = ctx.Filter.Indications
                let lbl = Terms.Indication |> getTerm "Indication"

                items |> responsiveFilter lbl sel indicationChange

        // Once a composition and indication offer more than one dose type, the dose type filter stays
        // visible after a selection, so the selection can be changed. Another composition or
        // indication resets this.
        let doseTypeChoice = React.useRef ((ctx.Filter.Generic, ctx.Filter.Indication), false)

        if fst doseTypeChoice.current <> (ctx.Filter.Generic, ctx.Filter.Indication) then
            doseTypeChoice.current <- (ctx.Filter.Generic, ctx.Filter.Indication), false

        if ctx.Filter.DoseTypes |> Array.length > 1 then
            doseTypeChoice.current <- (ctx.Filter.Generic, ctx.Filter.Indication), true

        let doseTypeFilter =
            if
                ctx.Filter.Generic.IsNone
                || ctx.Filter.Indication.IsNone
                || not (snd doseTypeChoice.current)
            then
                null
            else
                let sel = ctx.Filter.DoseType |> Option.map DoseTypeText.doseTypeToString
                let items = ctx.Filter.DoseTypes
                let lbl = Terms.``Dose Type`` |> getTerm "Dose type"

                items
                |> Array.map (fun s -> s |> DoseTypeText.doseTypeToString, s |> DoseTypeText.doseTypeToDescription)
                |> filterSelect lbl sel doseTypeChange

        {
            Label = label
            IsMobile = isMobile
            Order = displayOrder
            GenericFilter = genericFilter
            IndicationFilter = indicationFilter
            DoseTypeFilter = doseTypeFilter
            FrequencyControl = frequencyControl
            DoseQuantityControl = doseQtyControl
            DosePerTimeAdjustDisplay = dosePerTimeAdjDisplay
            RateControl = rateControl
            RateField = rateField props.fieldMinWidth
            TimeDisplay = timeDisplay props.fieldMinWidth
            TotalVolumeDisplay = totalVolumeDisplay
            ComponentHeader = headerRow
            ComponentRows = componentRows
            AdministrationHeading = administrationDivider
            ResetAction = resetAction
            ResetBar = resetBar
            SetDoseQuantityPerc = SetDoseQuantityPercProperty >> dispatch
            CanSetDoseShare = canSetDoseShare
            DoseQuantityShare = shownOrder |> Option.bind IntakePolicy.doseShare
            CompositionNotice = compositionNotice
            ScenarioNotice = scenarioNotice
        }
