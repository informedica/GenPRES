namespace Views

#nowarn "1104"

/// The logic of one nutrition slot, shared by the enteral and the parenteral layout: the
/// slot's messages and its trail, and a hook that turns the slot's order context into ready
/// controls.
module NutritionSlot =

    open Fable.Core
    open Fable.Core.JsInterop
    open Fable.React
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models
    open Shared.Models.Order
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


    module private Elmish =


        /// What the slot holds of its own: the component picked, never the order, which is
        /// the one the slot's context has.
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
            // Rate navigation
            | DecreaseDoseRateProperty of ntimes: int * useCalc: bool
            | IncreaseDoseRateProperty of ntimes: int * useCalc: bool
            | SetMinDoseRateProperty
            | SetMaxDoseRateProperty
            | SetMedianDoseRateProperty
            // Dose Quantity navigation
            | ChangeOrderableDoseQuantity of string option
            | DecreaseDoseQuantityProperty of ntimes: int * useCalc: bool
            | IncreaseDoseQuantityProperty of ntimes: int * useCalc: bool
            | SetMinDoseQuantityProperty
            | SetMaxDoseQuantityProperty
            | SetMedianDoseQuantityProperty
            | SetDoseQuantityPercProperty of perc: int
            // the move to 100% the slot makes by itself once the TPN is composed
            | StartDoseQuantityPercProperty
            // Component Quantity navigation (carries component name)
            | DecreaseComponentQuantityProperty of cmp: string * ntimes: int * useCalc: bool
            | IncreaseComponentQuantityProperty of cmp: string * ntimes: int * useCalc: bool
            | SetMinComponentQuantityProperty of cmp: string
            | SetMaxComponentQuantityProperty of cmp: string
            | SetMedianComponentQuantityProperty of cmp: string
            // Frequency navigation
            | DecreaseFrequencyProperty
            | IncreaseFrequencyProperty
            | SetMinFrequencyProperty
            | SetMaxFrequencyProperty
            | SetMedianFrequencyProperty


        /// The component picked, seeded from the one scenario of the slot's context: the one the
        /// slot kept for the same order, else the scenario's own, else its first component.
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
            // every change and every step is over the order shown, the slot's context's; the
            // plan lane holds it, the slot holds none
            let handleNav nav =
                match shown with
                | None -> state, Cmd.none
                | Some ord ->
                    OrderLoader.create state.SelectedComponent None ord |> nav
                    state, Cmd.none

            let handleNavWithCmp cmpName nav =
                match shown with
                | None -> state, Cmd.none
                | Some ord ->
                    OrderLoader.create (Some cmpName) None ord |> nav
                    state, Cmd.none

            // a pick of a variable of the order shown, sent to the plan lane; nothing to pick without one
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

            // Rate navigation
            | SetMinDoseRateProperty -> handleNav stepper.setRateMin
            | DecreaseDoseRateProperty(n, uc) -> handleNav (stepper.setRateDec (n, uc))
            | SetMedianDoseRateProperty -> handleNav stepper.setRateMed
            | IncreaseDoseRateProperty(n, uc) -> handleNav (stepper.setRateInc (n, uc))
            | SetMaxDoseRateProperty -> handleNav stepper.setRateMax

            // Dose Quantity navigation
            | SetMinDoseQuantityProperty -> handleNav stepper.setDoseQtyMin
            | DecreaseDoseQuantityProperty(n, uc) -> handleNav (stepper.setDoseQtyDec (n, uc))
            | SetMedianDoseQuantityProperty -> handleNav stepper.setDoseQtyMed
            | IncreaseDoseQuantityProperty(n, uc) -> handleNav (stepper.setDoseQtyInc (n, uc))
            | SetMaxDoseQuantityProperty -> handleNav stepper.setDoseQtyMax
            | SetDoseQuantityPercProperty perc -> handleNav (stepper.setDoseQtyPerc perc)
            | StartDoseQuantityPercProperty -> handleNav (stepper.setDoseQtyPerc 100)

            // Component Quantity navigation
            | SetMinComponentQuantityProperty cmp -> handleNavWithCmp cmp stepper.setComponentQtyMin
            | DecreaseComponentQuantityProperty(cmp, n, uc) -> handleNavWithCmp cmp (stepper.setComponentQtyDec (n, uc))
            | SetMedianComponentQuantityProperty cmp -> handleNavWithCmp cmp stepper.setComponentQtyMed
            | IncreaseComponentQuantityProperty(cmp, n, uc) -> handleNavWithCmp cmp (stepper.setComponentQtyInc (n, uc))
            | SetMaxComponentQuantityProperty cmp -> handleNavWithCmp cmp stepper.setComponentQtyMax

            // Frequency navigation
            | SetMinFrequencyProperty -> handleNav stepper.setFreqMin
            | DecreaseFrequencyProperty -> handleNav stepper.setFreqDec
            | SetMedianFrequencyProperty -> handleNav stepper.setFreqMed
            | IncreaseFrequencyProperty -> handleNav stepper.setFreqInc
            | SetMaxFrequencyProperty -> handleNav stepper.setFreqMax


    open Elmish


    /// What a message does: change a value, step it, send or reset the order, or change the slot alone.
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


    /// The field a message moves, a component field with its component.
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


    /// The message for the trail: its case, what it carries, and its field.
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


    /// The call a message makes, given the order shown and whether a reopen is under way.
    let private effectsOf (shown: Order option) (reopening: bool) msg =
        let shownId = shown |> Option.map (_.Id >> Trail.Part.shortId)

        match kindOf msg, shownId with
        | Kind.Local, _ -> []
        | Kind.Update, _ ->
            match msg with
            | UpdateOrderScenario _ ->
                let call = if reopening then "CallReopen" else "CallUpdate"
                let id = shownId |> Option.defaultValue "none"
                [ $"%s{call} %s{id}" ]
            | _ -> []
        | _, None -> []
        | Kind.Reset, Some id -> [ $"CallReset %s{id}" ]
        | Kind.Change, Some _ -> [ "ofMsg UpdateOrderScenario" ]
        | Kind.Step step, Some _ ->
            let field = fieldOf msg |> Option.defaultValue "?"
            [ $"CallStep %s{field} %s{step}" ]


    /// The slot's own state; a step names the component it acts on in its message.
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

    // the component rows: name, quantity and range in three columns, the quantity and the range
    // of even width, stacked below the large breakpoint, where the quantity column no longer
    // holds the quantity field's 400px
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

    // the quantity fields end in the column of their severity mark, so the middle column takes
    // as much room on its left, while the columns stand side by side
    let cmpMiddleSx =
        {|
            paddingLeft =
                {|
                    xs = "0px"
                    lg = $"%i{Components.QuantityField.markWidth}px"
                |}
        |}

    // the controls of the right column span it, so they are of even width; a field takes the
    // room of its severity mark within that width
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

    // the name stands in the first column, so the field's own caption is hidden; it stays in the
    // page for the screen reader, as the field also uses it as the id of its select
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

    let cmpSolutionSx = {| minWidth = 160 |}

    // stacked, the column headings are hidden, so the range carries its own caption
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


    // the administration of a scenario as pills, one per value the server printed, each in
    // its own severity
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
            /// Whether a change of the plan is under way.
            IsLoading: bool
            /// The order shown: the one scenario's of the slot's context.
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
            /// The dose per time, adjusted, shown only.
            DosePerTimeAdjustDisplay: JSX.Element
            /// The rate and the time side by side, for a timed or continuous order.
            RateControl: JSX.Element
            /// The rate field alone, at the least width of the fields, for a timed or continuous
            /// order.
            RateField: JSX.Element
            /// The time the order runs alone, at the least width of the fields, for a timed or
            /// continuous order.
            TimeDisplay: JSX.Element
            /// The total volume, shown only.
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
            /// The spinner shown while the slot has no order yet.
            LoadingIndicator: JSX.Element
            /// Set the dose quantity at a percentage of the total.
            SetDoseQuantityPerc: int -> unit
            /// Whether the intake slider can set the dose: the TPN is composed and its dose count
            /// holds one value.
            CanSetDoseQuantityPerc: bool
            /// The share of the total the dose is, in percent, when both hold one value.
            DoseQuantityShare: decimal option
            /// Why the components are locked, while the dose of the TPN is a part of the total.
            CompositionHint: JSX.Element
        }


    /// The controls of a slot, from its order context; every change goes to the plan as a
    /// command. A hook: call it at the top of a component, every render.
    let useSlot
        (props:
            {|
                nutritionContext: OrderContext
                plan: OrderPlan
                // a command into the slot's context, sent over the plan held
                planNavigate: string * Api.OrderContextCommand -> unit
                // a clear from a field's arrow, and the list of such a reopen closed without a pick
                planReopen: string * Api.OrderContextCommand -> unit
                planRestore: unit -> unit
                localizationTerms: Deferred<string[][]>
                isRecalculating: bool
                // the least width of the frequency, dose and dose per time fields
                fieldMinWidth: int option
            |})
        =
        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let getTerm = Global.getLocalizedTerm props.localizationTerms lang

        let ctx = props.nutritionContext
        let ncId = props.nutritionContext.Id

        // Monotonic counter bumped whenever a new server response replaces the order
        // context. Passed into stepped selects so they reset their optimistic step value
        // even when the server returns the SAME value as before (e.g. a no-op step when
        // already at the maximum), where the displayed value never changes and the
        // value-based reset alone would leave the stale optimistic value on screen.
        let revisionRef = React.useRef 0
        let prevCtxRef = React.useRef ctx

        if not (obj.ReferenceEquals(prevCtxRef.current, ctx)) then
            prevCtxRef.current <- ctx
            revisionRef.current <- revisionRef.current + 1

        let revision = revisionRef.current

        let label =
            let name = OrderContext.label ctx

            match OrderContext.nutritionCategory ctx with
            | Some NutritionCategory.EnteralFeeding -> Terms.``Nutrition Enteral Feeding`` |> getTerm name
            | Some NutritionCategory.EnteralSupplement -> Terms.``Nutrition Enteral Supplement`` |> getTerm name
            | Some NutritionCategory.TPN -> Terms.``Nutrition TPN`` |> getTerm name
            | Some NutritionCategory.Lipid -> Terms.``Nutrition Lipids`` |> getTerm name
            | Some NutritionCategory.ElectrolyteGlucose -> Terms.``Nutrition Electrolytes Glucose`` |> getTerm name
            | None -> name

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let fixPrecision = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision

        let markOf = ViewHelpers.markOf

        let navigate cmd = props.planNavigate (ncId, cmd)

        // a filter pick goes as its position in the options the field offers, an emptied field as
        // a clear; a value the field does not offer is a bug, written to the console and not sent
        let pickFilter field (options: 'a[]) (picked: 'a option) =
            match picked |> Option.map (fun x -> options |> Array.tryFindIndex ((=) x)) with
            | None -> navigate (Api.OrderContextCommand.ClearFilterProperty field)
            | Some(Some n) -> navigate (Api.OrderContextCommand.SetNthFilterProperty(field, n))
            | Some None -> Logging.warning "a pick the field does not offer is not sent" picked

        let genericChange s = pickFilter OrderContext.Generic ctx.Filter.Generics s

        let indicationChange s = pickFilter OrderContext.Indication ctx.Filter.Indications s

        let doseTypeChange s =
            let dt = s |> Option.map DoseType.doseTypeFromString
            pickFilter OrderContext.DoseType ctx.Filter.DoseTypes dt

        // set by a field's arrow just before it clears its value, so that the change goes out as
        // a reopen rather than as a change
        let reopening = React.useRef false

        // a pick goes out as the index of its value over the order as it is; a reopen as a clear
        // without picks, since only the workbench keeps them: the server clears and solves, so a
        // value another variable pins comes back as it was. A value the order does not offer is a
        // bug, written to the console and not sent
        let updateOrderScenario (ol: OrderLoader) target (s: string option) =
            let isReopen = reopening.current
            reopening.current <- false

            let located =
                target
                |> Option.bind (fun t -> ol.Order |> OrderContext.Target.tryGet t |> Option.map (fun v -> t, v))

            let send cmd =
                (ncId, cmd) |> if isReopen then props.planReopen else props.planNavigate

            match located, s with
            | Some(t, _), None when isReopen -> send (ViewHelpers.clearCommand t [||])
            | Some(t, ovar), Some key when not isReopen ->
                match OrderContext.values ovar |> Array.tryFindIndex ((=) key) with
                | Some n -> send (ViewHelpers.setNthCommand t n)
                | None -> Logging.warning "a value the field does not offer is not sent" key
            | _ -> Logging.warning "a change no field holds is not sent" s

        let resetOrderScenario (_ol: OrderLoader) = navigate Api.OrderContextCommand.ResetOrderScenario

        let stepper =
            let create cmd = fun (_: OrderLoader) -> navigate cmd

            let createWithN cmd = fun (n, uc) (_: OrderLoader) -> navigate (cmd (n, uc))

            let createWithCmp cmd =
                fun (ol: OrderLoader) -> ol.Component |> Option.iter (cmd >> navigate)

            let createWithCmpN cmd =
                fun (n, uc) (ol: OrderLoader) -> ol.Component |> Option.iter (fun c -> navigate (cmd (c, n, uc)))

            {|
                // Dose Rate
                setRateMin = create Api.OrderContextCommand.SetMinOrderableDoseRateProperty
                setRateDec = createWithN Api.OrderContextCommand.DecreaseOrderableDoseRateProperty
                setRateMed = create Api.OrderContextCommand.SetMedianOrderableDoseRateProperty
                setRateInc = createWithN Api.OrderContextCommand.IncreaseOrderableDoseRateProperty
                setRateMax = create Api.OrderContextCommand.SetMaxOrderableDoseRateProperty
                // Dose Quantity
                setDoseQtyMin = create Api.OrderContextCommand.SetMinOrderableDoseQuantityProperty
                setDoseQtyDec = createWithN Api.OrderContextCommand.DecreaseOrderableDoseQuantityProperty
                setDoseQtyMed = create Api.OrderContextCommand.SetMedianOrderableDoseQuantityProperty
                setDoseQtyInc = createWithN Api.OrderContextCommand.IncreaseOrderableDoseQuantityProperty
                setDoseQtyMax = create Api.OrderContextCommand.SetMaxOrderableDoseQuantityProperty
                setDoseQtyPerc = fun perc -> create (Api.OrderContextCommand.SetOrderableDoseQuantityPercProperty perc)
                // Component Quantity
                setComponentQtyMin = createWithCmp Api.OrderContextCommand.SetMinComponentOrderableQuantityProperty
                setComponentQtyDec = createWithCmpN Api.OrderContextCommand.DecreaseComponentOrderableQuantityProperty
                setComponentQtyMed = createWithCmp Api.OrderContextCommand.SetMedianComponentOrderableQuantityProperty
                setComponentQtyInc = createWithCmpN Api.OrderContextCommand.IncreaseComponentOrderableQuantityProperty
                setComponentQtyMax = createWithCmp Api.OrderContextCommand.SetMaxComponentOrderableQuantityProperty
                // Frequency
                setFreqMin = create Api.OrderContextCommand.SetMinScheduleFrequencyProperty
                setFreqDec = create Api.OrderContextCommand.DecreaseScheduleFrequencyProperty
                setFreqMed = create Api.OrderContextCommand.SetMedianScheduleFrequencyProperty
                setFreqInc = create Api.OrderContextCommand.IncreaseScheduleFrequencyProperty
                setFreqMax = create Api.OrderContextCommand.SetMaxScheduleFrequencyProperty
            |}

        // the order shown: the one scenario's of the slot's context
        let shownOrder = ctx.Scenarios |> Array.tryExactlyOne |> Option.map _.Order

        // the reopen flag is read before the update, which resets it
        let updateTraced msg state =
            let reopen = reopening.current
            let next, cmd = update updateOrderScenario resetOrderScenario stepper shownOrder msg state

            StepTrail.record (fun no at ->
                Trail.step "Nutrition" describeMsg id describeState no at msg (next, effectsOf shownOrder reopen msg)
            )

            next, cmd

        // the tab of the last render, so that an answer for the same order keeps it
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

        let isOrderLoading = props.isRecalculating
        let texts = ViewHelpers.quantityFieldTexts getTerm

        let select = ViewHelpers.orderSelect texts true isOrderLoading

        let isTpn = ctx |> isOneOf [ NutritionCategory.TPN ]

        // the order and composition the move to 100% was sent for
        let sentFor = React.useRef (None: (string * decimal option[]) option)

        // once the TPN is composed, its dose is set at the whole total, so the dose count holds one
        // value and the intake slider can take over
        let startIntake () =
            match shownOrder with
            | Some ord when isTpn && not isOrderLoading ->
                match ord |> IntakePolicy.start sentFor.current with
                | IntakePolicy.Start.Clear -> sentFor.current <- None
                | IntakePolicy.Start.Wait -> ()
                | IntakePolicy.Start.Send key ->
                    sentFor.current <- Some key
                    // run directly: a dispatch from here reaches the program useElmish is replacing
                    updateTraced StartDoseQuantityPercProperty state |> ignore
            | _ -> ()

        React.useEffect (startIntake, [| box ctx; box isOrderLoading |])

        // while the dose of the TPN is a part of the total, the composition stays as it is: a step
        // of a component would keep the rate of the part for the whole
        let isCompositionLocked = isTpn && shownOrder |> Option.exists IntakePolicy.isCompositionLocked

        let componentSelect = ViewHelpers.orderSelect texts true (isOrderLoading || isCompositionLocked)

        // a field with one value reopens by its arrow; without picks it cannot tell whether the user
        // constrained it
        let reopenOf (ovar: OrderVariable) : ViewHelpers.Reopen =
            {|
                constrained = PickList.constrained None ovar.Name
                reopening = fun () -> reopening.current <- true
                restore = props.planRestore
                busy = isOrderLoading
            |}
        // a value only shown has nothing for a cross to clear
        let display = ViewHelpers.orderFixed texts true isOrderLoading
        let filterSelect = ViewHelpers.filterSelect isOrderLoading isOrderLoading
        let responsiveFilter = ViewHelpers.responsiveFilter isMobile isOrderLoading isOrderLoading
        let loadingIndicator = ViewHelpers.inlineProgress isOrderLoading

        let displayOrder = shownOrder

        let componentRows =
            match displayOrder with
            | Some ord ->
                let cmps = ord.Orderable.Components
                let lastIdx = cmps.Length - 1

                cmps
                |> Array.mapi (fun i cmp ->
                    let qtyVals = cmp.OrderableQuantity |> ViewHelpers.ovarValsWithRange string 3

                    let nav =
                        let cmpName = cmp.Name

                        let mode =
                            cmp.OrderableQuantity
                            |> QuantityModePolicy.decideFor QuantityModePolicy.Field.ComponentQuantity ord

                        ViewHelpers.createStepper
                            dispatch
                            revision
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

                    // the label is hidden from sight but kept: the field uses it as the id of its select
                    let qtyLabel = cmp.OrderableQuantity |> ViewHelpers.ovarLabel cmp.Name

                    let qtyControl =
                        componentSelect
                            false
                            qtyLabel
                            None
                            (fun s -> ChangeComponentOrderableQuantity(cmp.Name, s) |> dispatch)
                            nav
                            qtyWarning
                            (reopenOf cmp.OrderableQuantity)
                            (Some 400)
                            qtyVals

                    // a component is in the mix once its quantity is chosen and above zero; a list of
                    // values still to pick from does not include it
                    let isIncluded =
                        cmp.OrderableQuantity.Variable.Vals
                        |> Option.exists (fun vu ->
                            match vu.Value with
                            | [| _, d |] -> d > 0m
                            | _ -> false
                        )

                    let nameColor = if isIncluded then "text.primary" else "text.disabled"

                    let name =
                        JSX.jsx
                            $"""
                        import Typography from '@mui/material/Typography';
                        <Typography variant="body1" color={nameColor}>{cmp.Name}</Typography>
                        """

                    // the first component is the composition and the last the solution: neither can be
                    // left out; the checkbox and the solution choice are shown, not yet working
                    let labelCell =
                        if i = 0 then
                            name
                        elif i = lastIdx then
                            JSX.jsx
                                $"""
                            import Select from '@mui/material/Select';
                            import MenuItem from '@mui/material/MenuItem';
                            <Select variant="standard" value={cmp.Name} disabled={true} sx={cmpSolutionSx}>
                                <MenuItem value={cmp.Name}>{cmp.Name}</MenuItem>
                            </Select>
                            """
                        else
                            JSX.jsx
                                $"""
                            import Stack from '@mui/material/Stack';
                            import Checkbox from '@mui/material/Checkbox';
                            <Stack direction="row" sx={alignCenterSx}>
                                <Checkbox checked={isIncluded} disabled={true} />
                                {name}
                            </Stack>
                            """

                    // the range the solver calculated for the component's quantity, from the rules
                    // that bound it
                    let range, rangeCaption =
                        match cmp.OrderableQuantity.CalculatedConstraints |> Variable.renderValue 3 with
                        | "" -> "", ""
                        | r -> $"(%s{r})", "Aanbevolen range"

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
                            <Typography variant="body1" color={nameColor}>{range}</Typography>
                        </Grid>
                    </Grid>
                    """
                )
            | None -> [| null |]

        let selectMinWidth = props.fieldMinWidth

        let doseQtyControl =
            match displayOrder with
            | Some ord ->
                let severity = ord.Orderable.Dose.Quantity |> markOf

                let label = ord.Orderable.Dose.Quantity |> ViewHelpers.ovarLabel "toedien hoeveelheid"

                let vals = ord.Orderable.Dose.Quantity |> ViewHelpers.ovarValsWithRange string 3

                let doseQtyNav =
                    ViewHelpers.createDoseQtyStepper
                        dispatch
                        revision
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
                    (reopenOf ord.Orderable.Dose.Quantity)
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
                        revision
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
                    (reopenOf ord.Schedule.Frequency)
                    selectMinWidth
                    freqVals
            | _ -> null

        let genericFilter =
            let sel = ctx.Filter.Generic
            let items = ctx.Filter.Generics
            let lbl = Terms.Composition |> getTerm "Samenstelling"

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
                        revision
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
                    (reopenOf ord.Orderable.Dose.Rate)
                    minWidth
            | _ -> null

        // the time the order runs, shown only, for a timed or continuous order
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

                ovar
                |> ViewHelpers.ovarValsWithRange string 3
                |> display
                    false
                    (ovar |> ViewHelpers.ovarLabel "totaal volume")
                    None
                    ignore
                    ViewHelpers.noSteps
                    (ovar |> markOf)
                    (Some 400)
            | None -> null

        let onClickReset = fun () -> ResetOrderScenario |> dispatch

        let heading label =
            Components.SectionHeading.View
                {|
                    label = label
                    action = None
                |}

        let administrationDivider = Terms.``Prescribe Administration`` |> getTerm "toediening" |> heading

        let componentHeading = heading "Component"
        let quantityHeading = heading "Hoeveelheid"
        let rangeHeading = heading "Aanbevolen range"

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

        // the changes to the order are discarded: bounded and to the left, so the button is not
        // as wide as the panel it sits in and is not where you click by default
        let resetAction =
            {|
                label = Terms.Reset |> getTerm "Reset"
                kind = Components.ActionBar.Kind.Secondary
                onClick = onClickReset
                disabled = isOrderLoading
                icon = Some Mui.Icons.RefreshIcon
            |}

        let resetBar = Components.ActionBar.View {| actions = [| resetAction |] |}

        let compositionHint =
            if isCompositionLocked then
                JSX.jsx
                    $"""
                import Typography from '@mui/material/Typography';
                <Typography variant="caption" color="text.secondary">
                    zet de toedien hoeveelheid eerst op 100%% om de samenstelling te wijzigen
                </Typography>
                """
            else
                null

        let canSetDoseQuantityPerc = shownOrder |> Option.exists IntakePolicy.canSetDoseQuantityPerc

        let indicationFilter =
            if ctx.Filter.Generic.IsNone || ctx.Filter.Indications |> Array.length <= 1 then
                null
            else
                let sel = ctx.Filter.Indication
                let items = ctx.Filter.Indications
                let lbl = Terms.Indication |> getTerm "Indicatie"

                items |> responsiveFilter lbl sel indicationChange

        // once a composition and indication offered more than one dose type, the choice stays in
        // view after one is picked, so it can be changed; another composition or indication
        // starts over
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
                let sel = ctx.Filter.DoseType |> Option.map DoseType.doseTypeToString
                let items = ctx.Filter.DoseTypes
                let lbl = Terms.``Dose Type`` |> getTerm "Doseer type"

                items
                |> Array.map (fun s -> s |> DoseType.doseTypeToString, s |> DoseType.doseTypeToDescription)
                |> filterSelect lbl sel doseTypeChange

        {
            Label = label
            IsMobile = isMobile
            IsLoading = isOrderLoading
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
            LoadingIndicator = loadingIndicator
            SetDoseQuantityPerc = SetDoseQuantityPercProperty >> dispatch
            CanSetDoseQuantityPerc = canSetDoseQuantityPerc
            DoseQuantityShare = shownOrder |> Option.bind IntakePolicy.doseShare
            CompositionHint = compositionHint
        }
