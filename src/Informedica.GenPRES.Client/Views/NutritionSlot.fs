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
            | UpdateOrderScenario of Order
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


        /// The component picked, seeded from the one scenario of the slot's context: its first
        /// component.
        let init (ctx: OrderContext) =
            let cmp =
                match ctx.Scenarios with
                | [| sc |] ->
                    match sc.Order.Orderable.Components with
                    | [||] -> None
                    | cmps -> Some cmps[0].Name
                | _ ->
                    if ctx.Scenarios |> Array.length > 1 then
                        Logging.error "received multiple scenarios" ctx.Scenarios.Length

                    None

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
            let setOvar = OrderVariable.setOvar

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

            match msg with

            | UpdateOrderScenario ord ->
                OrderLoader.create state.SelectedComponent None ord |> updateOrderScenario

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
                match shown with
                | Some ord ->
                    let msg =
                        { ord with
                            Order.Orderable.Components =
                                ord.Orderable.Components
                                |> Array.map (fun cmp ->
                                    if cmp.Name = cmpName then
                                        { cmp with OrderableQuantity = cmp.OrderableQuantity |> setOvar s }
                                    else
                                        cmp
                                )
                        }
                        |> UpdateOrderScenario

                    state, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeComponentDoseQuantityAdjust(cmpName, s) ->
                match shown with
                | Some ord ->
                    let msg =
                        { ord with
                            Order.Orderable.Components =
                                ord.Orderable.Components
                                |> Array.map (fun cmp ->
                                    if cmp.Name = cmpName then
                                        { cmp with
                                            Component.Dose.QuantityAdjust = cmp.Dose.QuantityAdjust |> setOvar s
                                        }
                                    else
                                        cmp
                                )
                        }
                        |> UpdateOrderScenario

                    state, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableDoseRate s ->
                match shown with
                | Some ord ->
                    let msg =
                        { ord with Order.Orderable.Dose.Rate = ord.Orderable.Dose.Rate |> setOvar s }
                        |> UpdateOrderScenario

                    state, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableQuantity s ->
                match shown with
                | Some ord ->
                    let msg =
                        { ord with Order.Orderable.OrderableQuantity = ord.Orderable.OrderableQuantity |> setOvar s }
                        |> UpdateOrderScenario

                    state, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeFrequency s ->
                match shown with
                | Some ord ->
                    let msg =
                        { ord with Order.Schedule.Frequency = ord.Schedule.Frequency |> setOvar s }
                        |> UpdateOrderScenario

                    state, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableDoseQuantity s ->
                match shown with
                | Some ord ->
                    let msg =
                        { ord with Order.Orderable.Dose.Quantity = ord.Orderable.Dose.Quantity |> setOvar s }
                        |> UpdateOrderScenario

                    state, Cmd.ofMsg msg
                | _ -> state, Cmd.none

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
        | SetMaxDoseQuantityProperty -> Some "ordDoseQty"
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
            | UpdateOrderScenario ord -> $"UpdateOrderScenario %s{Trail.Part.shortId ord.Id}"
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
            | UpdateOrderScenario ord ->
                let call = if reopening then "CallReopen" else "CallUpdate"
                [ $"%s{call} %s{Trail.Part.shortId ord.Id}" ]
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

    // the component rows: name, quantity and range in three columns, stacked below the large
    // breakpoint, where five twelfths of the slot no longer holds the quantity field's 400px
    let cmpLabelSize =
        {|
            xs = 12
            lg = 4
        |}

    let cmpQtySize =
        {|
            xs = 12
            lg = 5
        |}

    let cmpRangeSize =
        {|
            xs = 12
            lg = 3
        |}

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
            /// The rate and the time, for a timed or continuous order.
            RateControl: JSX.Element
            /// The total volume, shown only.
            TotalVolumeDisplay: JSX.Element
            /// The headings above the component rows.
            ComponentHeader: JSX.Element
            /// A row per component: its name, its quantity and the recommended range.
            ComponentRows: JSX.Element[]
            /// The heading above the administration fields.
            AdministrationHeading: JSX.Element
            /// The button that discards the changes to the order.
            ResetBar: JSX.Element
            /// The spinner shown while the slot has no order yet.
            LoadingIndicator: JSX.Element
        }


    /// The controls of a slot, from its order context; every change goes to the plan as a
    /// command. A hook: call it at the top of a component, every render.
    let useSlot
        (props:
            {|
                nutritionContext: OrderContext
                plan: OrderPlan
                planCommand: Api.OrderPlanCommand -> unit
                // a clear from a field's arrow, and the list of such a reopen closed without a pick
                planReopen: Api.OrderPlanCommand -> unit
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

        // Use a ref for the plan so that closures captured by useElmish
        // always read the latest plan, even when useElmish doesn't re-initialize
        // (its deps only include ctx, not the plan).
        let planRef = React.useRef props.plan
        planRef.current <- props.plan

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let fixPrecision = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision

        let markOf = ViewHelpers.markOf

        let genericChange s =
            ctx
            |> OrderContext.medicationChange s
            |> fun updCtx ->
                Api.OrderPlanCommand.Navigate(planRef.current, ncId, Api.OrderContextCommand.UpdateOrderContext, updCtx)
            |> props.planCommand

        let indicationChange s =
            ctx
            |> OrderContext.indicationChange s
            |> fun updCtx ->
                Api.OrderPlanCommand.Navigate(planRef.current, ncId, Api.OrderContextCommand.UpdateOrderContext, updCtx)
            |> props.planCommand

        let doseTypeChange s =
            let dt = s |> Option.map DoseType.doseTypeFromString

            ctx
            |> OrderContext.doseTypeChange dt
            |> fun updCtx ->
                Api.OrderPlanCommand.Navigate(planRef.current, ncId, Api.OrderContextCommand.UpdateOrderContext, updCtx)
            |> props.planCommand

        // set by a field's arrow just before it clears its value, so that the change goes out as
        // a reopen rather than as a change
        let reopening = React.useRef false

        // a reopen goes without picks, since only the workbench keeps them: the server clears and
        // solves, so a value another variable pins comes back as it was
        let updateOrderScenario (ol: OrderLoader) =
            let isReopen = reopening.current
            reopening.current <- false

            let cmd =
                if isReopen then
                    Api.OrderContextCommand.ReopenOrderScenario [||]
                else
                    Api.OrderContextCommand.UpdateOrderScenario

            ViewHelpers.withLoader ctx ol
            |> fun updCtx ->
                Api.OrderPlanCommand.Navigate(planRef.current, ncId, cmd, updCtx)
                |> if isReopen then props.planReopen else props.planCommand

        let resetOrderScenario (_ol: OrderLoader) =
            Api.OrderPlanCommand.Navigate(planRef.current, ncId, Api.OrderContextCommand.ResetOrderScenario, ctx)
            |> props.planCommand

        let stepper =
            let create nav = fun ol -> ViewHelpers.withLoader ctx ol |> nav

            let createWithN nav = fun (n, uc) ol -> nav (ViewHelpers.withLoader ctx ol, n, uc)

            let createWithCmp nav =
                fun (ol: OrderLoader) ->
                    match ol.Component with
                    | None -> ()
                    | Some cmp -> nav (ViewHelpers.withLoader ctx ol, cmp)

            let createWithCmpN nav =
                fun (n, uc) (ol: OrderLoader) ->
                    match ol.Component with
                    | None -> ()
                    | Some cmp -> nav (ViewHelpers.withLoader ctx ol, cmp, n, uc)

            let navRate cmd =
                fun updCtx ->
                    Api.OrderPlanCommand.Navigate(planRef.current, ncId, cmd, updCtx)
                    |> props.planCommand

            let navRateN cmd =
                fun (updCtx, n, uc) ->
                    Api.OrderPlanCommand.Navigate(planRef.current, ncId, cmd (n, uc), updCtx)
                    |> props.planCommand

            let navCmpQty cmd =
                fun (updCtx, cmp) ->
                    Api.OrderPlanCommand.Navigate(planRef.current, ncId, cmd cmp, updCtx)
                    |> props.planCommand

            let navCmpQtyN cmd =
                fun (updCtx, cmp, n, uc) ->
                    Api.OrderPlanCommand.Navigate(planRef.current, ncId, cmd (cmp, n, uc), updCtx)
                    |> props.planCommand

            {|
                // Dose Rate
                setRateMin = create (navRate Api.OrderContextCommand.SetMinOrderableDoseRateProperty)
                setRateDec = createWithN (navRateN Api.OrderContextCommand.DecreaseOrderableDoseRateProperty)
                setRateMed = create (navRate Api.OrderContextCommand.SetMedianOrderableDoseRateProperty)
                setRateInc = createWithN (navRateN Api.OrderContextCommand.IncreaseOrderableDoseRateProperty)
                setRateMax = create (navRate Api.OrderContextCommand.SetMaxOrderableDoseRateProperty)
                // Dose Quantity
                setDoseQtyMin = create (navRate Api.OrderContextCommand.SetMinOrderableDoseQuantityProperty)
                setDoseQtyDec = createWithN (navRateN Api.OrderContextCommand.DecreaseOrderableDoseQuantityProperty)
                setDoseQtyMed = create (navRate Api.OrderContextCommand.SetMedianOrderableDoseQuantityProperty)
                setDoseQtyInc = createWithN (navRateN Api.OrderContextCommand.IncreaseOrderableDoseQuantityProperty)
                setDoseQtyMax = create (navRate Api.OrderContextCommand.SetMaxOrderableDoseQuantityProperty)
                // Component Quantity
                setComponentQtyMin =
                    createWithCmp (navCmpQty Api.OrderContextCommand.SetMinComponentOrderableQuantityProperty)
                setComponentQtyDec =
                    createWithCmpN (navCmpQtyN Api.OrderContextCommand.DecreaseComponentOrderableQuantityProperty)
                setComponentQtyMed =
                    createWithCmp (navCmpQty Api.OrderContextCommand.SetMedianComponentOrderableQuantityProperty)
                setComponentQtyInc =
                    createWithCmpN (navCmpQtyN Api.OrderContextCommand.IncreaseComponentOrderableQuantityProperty)
                setComponentQtyMax =
                    createWithCmp (navCmpQty Api.OrderContextCommand.SetMaxComponentOrderableQuantityProperty)
                // Frequency
                setFreqMin = create (navRate Api.OrderContextCommand.SetMinScheduleFrequencyProperty)
                setFreqDec = create (navRate Api.OrderContextCommand.DecreaseScheduleFrequencyProperty)
                setFreqMed = create (navRate Api.OrderContextCommand.SetMedianScheduleFrequencyProperty)
                setFreqInc = create (navRate Api.OrderContextCommand.IncreaseScheduleFrequencyProperty)
                setFreqMax = create (navRate Api.OrderContextCommand.SetMaxScheduleFrequencyProperty)
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

        let state, dispatch = React.useElmish (init ctx, updateTraced, [| box ctx |])

        let isOrderLoading = props.isRecalculating
        let texts = ViewHelpers.quantityFieldTexts getTerm

        let select = ViewHelpers.orderSelect texts true isOrderLoading

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
                        select
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

                    let range, rangeCaption =
                        match cmp.OrderableQuantity.DefinedConstraints |> Variable.renderValue 3 with
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
                        <Grid size={cmpQtySize}>
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

        let rateControl =
            match displayOrder with
            | Some ord when ord.Schedule.IsTimed || ord.Schedule.IsContinuous ->
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

                let rateDisplay =
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
                        (Some 400)

                let timeDisplay =
                    ord.Schedule.Time
                    |> ViewHelpers.ovarDisplay display "looptijd" (fixPrecision 3) (Some 400)

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

        let totalVolumeDisplay =
            match displayOrder with
            | Some ord ->
                ord.Orderable.OrderableQuantity
                |> ViewHelpers.ovarDisplay display "totaal volume" string (Some 400)
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
                <Grid size={cmpQtySize}>
                    {quantityHeading}
                </Grid>
                <Grid size={cmpRangeSize}>
                    {rangeHeading}
                </Grid>
            </Grid>
            """

        // the changes to the order are discarded: bounded and to the left, so the button is not
        // as wide as the panel it sits in and is not where you click by default
        let resetBar =
            Components.ActionBar.View
                {|
                    actions =
                        [|
                            {|
                                label = Terms.Reset |> getTerm "Reset"
                                kind = Components.ActionBar.Kind.Secondary
                                onClick = onClickReset
                                disabled = isOrderLoading
                                icon = Some Mui.Icons.RefreshIcon
                            |}
                        |]
                |}

        let indicationFilter =
            if ctx.Filter.Generic.IsNone || ctx.Filter.Indications |> Array.length <= 1 then
                null
            else
                let sel = ctx.Filter.Indication
                let items = ctx.Filter.Indications
                let lbl = Terms.Indication |> getTerm "Indicatie"

                items |> responsiveFilter lbl sel indicationChange

        let doseTypeFilter =
            if
                ctx.Filter.Generic.IsNone
                || ctx.Filter.Indication.IsNone
                || ctx.Filter.DoseTypes |> Array.length <= 1
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
            TotalVolumeDisplay = totalVolumeDisplay
            ComponentHeader = headerRow
            ComponentRows = componentRows
            AdministrationHeading = administrationDivider
            ResetBar = resetBar
            LoadingIndicator = loadingIndicator
        }
