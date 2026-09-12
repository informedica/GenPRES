namespace Views

#nowarn "1104"

module Nutrition =

    open Fable.Core
    open Fable.React
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models
    open Shared.Models.Order
    open Elmish
    open Utils
    open FSharp.Core


    module private Elmish =


        type State =
            {
                Order: Order option
                SelectedComponent: string option
            }

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


        let init (ctx: Deferred<OrderContext>) =
            let ord, cmp =
                match ctx with
                | Resolved ctx
                | Recalculating ctx ->
                    match ctx.Scenarios with
                    | [| sc |] ->
                        let ord = sc.Order

                        match ord.Orderable.Components with
                        | [||] -> Some ord, None
                        | cmps -> Some ord, Some cmps[0].Name
                    | _ ->
                        if ctx.Scenarios |> Array.length > 1 then
                            Logging.error "received multiple scenarios" ctx.Scenarios.Length

                        None, None
                | _ -> None, None

            {
                SelectedComponent = cmp
                Order = ord
            },
            Cmd.none


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
                |})
            (msg: Msg)
            (state: State)
            : State * Cmd<Msg>
            =
            let setOvar = OrderVariable.setOvar

            let handleNav nav =
                match state.Order with
                | None -> state, Cmd.none
                | Some ord ->
                    OrderLoader.create state.SelectedComponent None ord |> nav
                    { state with Order = None }, Cmd.none

            let handleNavWithCmp cmpName nav =
                match state.Order with
                | None -> state, Cmd.none
                | Some ord ->
                    OrderLoader.create (Some cmpName) None ord |> nav
                    { state with Order = None }, Cmd.none

            match msg with

            | UpdateOrderScenario ord ->
                OrderLoader.create state.SelectedComponent None ord |> updateOrderScenario

                { state with Order = None }, Cmd.none

            | ResetOrderScenario ->
                match state.Order with
                | Some ord -> OrderLoader.create state.SelectedComponent None ord |> resetOrderScenario
                | None -> ()

                { state with Order = None }, Cmd.none

            | ChangeComponent cmp ->
                match cmp with
                | None -> state, Cmd.none
                | Some _ -> { state with SelectedComponent = cmp }, Cmd.none

            | ChangeComponentOrderableQuantity(cmpName, s) ->
                match state.Order with
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

                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeComponentDoseQuantityAdjust(cmpName, s) ->
                match state.Order with
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

                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableDoseRate s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with Order.Orderable.Dose.Rate = ord.Orderable.Dose.Rate |> setOvar s }
                        |> UpdateOrderScenario

                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with Order.Orderable.OrderableQuantity = ord.Orderable.OrderableQuantity |> setOvar s }
                        |> UpdateOrderScenario

                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeFrequency s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with Order.Schedule.Frequency = ord.Schedule.Frequency |> setOvar s }
                        |> UpdateOrderScenario

                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableDoseQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with Order.Orderable.Dose.Quantity = ord.Orderable.Dose.Quantity |> setOvar s }
                        |> UpdateOrderScenario

                    { state with Order = None }, Cmd.ofMsg msg
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


    open Elmish


    let private halfSize =
        {|
            xs = 12
            md = 6
        |}

    let private cellSx =
        {|
            minWidth = 350
            ``& .MuiFormControl-root`` = {| width = "100%" |}
        |}

    let private flexEndSx = {| alignItems = "flex-end" |}

    let private alignCenterSx = {| alignItems = "center" |}

    let private boldCellSx = {| fontWeight = "bold" |}

    let private autoMarginSx = {| marginLeft = "auto" |}

    let private flexOverflowSx =
        {|
            display = "flex"
            alignItems = "center"
            width = "100%"
            overflow = "hidden"
        |}

    let private printSectionHeaderSx =
        {|
            fontWeight = "bold"
            backgroundColor = "#f5f5f5"
            padding = "4px 8px"
            borderRadius = 1
        |}

    let private printSectionHeaderMb1Sx =
        {|
            fontWeight = "bold"
            backgroundColor = "#f5f5f5"
            padding = "4px 8px"
            borderRadius = 1
            marginBottom = 1
        |}

    let private printSectionHeaderMb2Sx =
        {|
            fontWeight = "bold"
            backgroundColor = "#f5f5f5"
            padding = "4px 8px"
            borderRadius = 1
            marginBottom = 2
        |}

    let private removeButtonSx =
        {|
            marginLeft = "auto"
            display = "inline-flex"
            alignItems = "center"
            cursor = "pointer"
            padding = "4px"
            borderRadius = "50%"
            ``&:hover`` = {| backgroundColor = "rgba(0, 0, 0, 0.04)" |}
        |}

    let private addButtonSx =
        {|
            marginTop = 1
            marginBottom = 1
        |}

    let private dividerSx =
        {|
            marginTop = 2
            marginBottom = 2
        |}


    let private renderAdminSummary (key: string) (name: string) (blocks: TextBlock[]) =
        let typoSx =
            {|
                display = "inline"
                color = "text.secondary"
            |}

        let boxSx =
            {|
                display = "inline"
                marginLeft = 1
            |}

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Box key={key} sx={boxSx}>
            <Typography variant="body2" sx={typoSx}>
                {name}:
            </Typography>
            {blocks
             |> Array.map Mui.TypoGraphy.fromTextBlock
             |> unbox<seq<ReactElement>>
             |> React.Fragment}
        </Box>
        """


    [<JSX.Component>]
    let private ParenteralPrintView
        (props:
            {|
                plan: OrderPlan
                onClose: unit -> unit
            |})
        =
        let weightKg = ViewHelpers.PrintView.patientWeight (props.plan.Patient |> Some)

        let parenteralContexts =
            props.plan.NutritionContexts
            |> Array.filter (fun nc ->
                nc.Category = NutritionCategory.TPN
                || nc.Category = NutritionCategory.Lipid
                || nc.Category = NutritionCategory.ElectrolyteGlucose
            )

        let tableSx =
            {|
                tableLayout = "fixed"
                width = "100%"
            |}

        let contextSections =
            parenteralContexts
            |> Array.map (fun nc ->
                let scenario = nc.OrderContext.Scenarios |> Array.tryExactlyOne

                match scenario with
                | None ->
                    let mb2Sx = {| marginBottom = 2 |}

                    JSX.jsx
                        $"""
                    import Box from '@mui/material/Box';
                    import Typography from '@mui/material/Typography';

                    <Box key={nc.Id} sx={mb2Sx}>
                        <Typography variant="subtitle1" sx={printSectionHeaderSx}>
                            {nc.Label}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                            niet geconfigureerd
                        </Typography>
                    </Box>
                    """
                | Some sc ->
                    let ord = sc.Order
                    let orderableName = ord.Orderable.Name

                    let componentRows =
                        ord.Orderable.Components
                        |> Array.map (fun cmp ->
                            let cmpQty = cmp.OrderableQuantity |> OrderVariable.displayString
                            let fixPrec2 = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 2

                            let doseAdj =
                                cmp.Dose.QuantityAdjust |> OrderVariable.displayStringFormatted fixPrec2

                            JSX.jsx
                                $"""
                            import TableRow from '@mui/material/TableRow';
                            import TableCell from '@mui/material/TableCell';

                            <TableRow key={cmp.Name}>
                                <TableCell colSpan={2}>{cmp.Name}</TableCell>
                                <TableCell align="right"><strong>{cmpQty}</strong></TableCell>
                                <TableCell align="right">{doseAdj}</TableCell>
                            </TableRow>
                            """
                        )

                    let totalVolume = ord.Orderable.OrderableQuantity |> OrderVariable.displayString

                    let rateDisplay =
                        if ord.Schedule.IsContinuous || ord.Schedule.IsTimed then
                            let rate = ord.Orderable.Dose.Rate |> OrderVariable.displayString
                            let fixPrec2 = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 2
                            let time = ord.Schedule.Time |> OrderVariable.displayStringFormatted fixPrec2

                            let rateBoxSx =
                                {|
                                    display = "flex"
                                    gap = 3
                                    marginTop = 2
                                |}

                            JSX.jsx
                                $"""
                            import Typography from '@mui/material/Typography';
                            import Box from '@mui/material/Box';

                            <Box sx={rateBoxSx}>
                                <Typography variant="body2">
                                    Pompsnelheid: <strong>{rate}</strong>
                                </Typography>
                                <Typography variant="body2">
                                    Looptijd: <strong>{time}</strong>
                                </Typography>
                            </Box>
                            """
                        else
                            null

                    JSX.jsx
                        $"""
                    import Box from '@mui/material/Box';
                    import Typography from '@mui/material/Typography';
                    import Table from '@mui/material/Table';
                    import TableBody from '@mui/material/TableBody';
                    import TableRow from '@mui/material/TableRow';
                    import TableCell from '@mui/material/TableCell';
                    import TableHead from '@mui/material/TableHead';

                    <Box key={nc.Id} sx={ {| marginBottom = 3 |} }>
                        <Typography variant="subtitle1" sx={printSectionHeaderMb1Sx}>
                            {nc.Label} - {orderableName}
                        </Typography>
                        <Table size="small" sx={tableSx}>
                            <TableHead>
                                <TableRow>
                                    <TableCell colSpan={2}><strong>Component</strong></TableCell>
                                    <TableCell align="right"><strong>Volume</strong></TableCell>
                                    <TableCell align="right"><strong>Dosis/kg</strong></TableCell>
                                </TableRow>
                            </TableHead>
                            <TableBody>
                                {componentRows |> unbox<seq<ReactElement>> |> React.Fragment}
                                <TableRow>
                                    <TableCell colSpan={2} sx={boldCellSx}>Totaal volume</TableCell>
                                    <TableCell align="right" sx={boldCellSx}>{totalVolume}</TableCell>
                                    <TableCell />
                                </TableRow>
                            </TableBody>
                        </Table>
                        {rateDisplay}
                    </Box>
                    """
            )

        let totalsSection =
            let intake = props.plan.Totals
            let rows = Totals.intakeRows

            let activeRows =
                rows
                |> Array.filter (fun cells ->
                    let name = cells |> Array.head
                    let items = Totals.substanceToField intake name
                    items |> Array.length >= 2
                )

            let totalsRows =
                activeRows
                |> Array.map (fun cells ->
                    let name = cells[0]
                    let unit = cells[2]
                    let items = Totals.substanceToField intake name

                    let value =
                        if items.Length >= 2 then
                            items[0 .. items.Length - 2]
                            |> Array.map (fun item ->
                                match item with
                                | Normal s
                                | Bold s
                                | Italic s -> s
                            )
                            |> String.concat " "
                        else
                            ""

                    let normal =
                        if items.Length >= 1 then
                            let s =
                                match items[items.Length - 1] with
                                | Normal s
                                | Bold s
                                | Italic s -> s

                            if s = "" then "" else s + " " + unit
                        else
                            ""

                    JSX.jsx
                        $"""
                    import TableRow from '@mui/material/TableRow';
                    import TableCell from '@mui/material/TableCell';

                    <TableRow key={name}>
                        <TableCell colSpan={2}>{name}</TableCell>
                        <TableCell align="right">{value}</TableCell>
                        <TableCell align="right">{normal}</TableCell>
                    </TableRow>
                    """
                )

            if activeRows.Length = 0 then
                null
            else
                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import Typography from '@mui/material/Typography';
                import Table from '@mui/material/Table';
                import TableBody from '@mui/material/TableBody';

                <Box sx={ {| marginTop = 2 |} }>
                    <Typography variant="subtitle1" sx={printSectionHeaderMb1Sx}>
                        Totalen
                    </Typography>
                    <Table size="small" sx={tableSx}>
                        <TableBody>
                            {totalsRows |> unbox<seq<ReactElement>> |> React.Fragment}
                        </TableBody>
                    </Table>
                </Box>
                """

        let printContent =
            JSX.jsx
                $"""
            import Typography from '@mui/material/Typography';

            <React.Fragment>
                <Typography variant="subtitle1" sx={printSectionHeaderMb2Sx}>
                    INFUUS AFSPRAKEN CENTRAAL VENEUZE CATHETERS
                </Typography>
                {ViewHelpers.PrintView.PatientHeader {| weightKg = weightKg |}}
                {contextSections |> unbox<seq<ReactElement>> |> React.Fragment}
                {totalsSection}
                {ViewHelpers.PrintView.PatientSignature()}
            </React.Fragment>
            """
            |> toReact

        ViewHelpers.PrintView.PrintDialog
            {|
                isOpen = true
                onClose = props.onClose
                title = "Parenterale Voeding"
                children = printContent
            |}


    [<JSX.Component>]
    let private NutritionSlot
        (props:
            {|
                nutritionContext: NutritionContext
                plan: OrderPlan
                planCommand: Api.PlanCommand -> unit
                localizationTerms: Deferred<string[][]>
                onRemove: (unit -> unit) option
                wrapInAccordion: bool
                isRecalculating: bool
            |})
        =
        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let getTerm = Global.getLocalizedTerm props.localizationTerms lang

        let ctx = props.nutritionContext.OrderContext
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
            match props.nutritionContext.Category with
            | NutritionCategory.EnteralFeeding ->
                Terms.``Nutrition Enteral Feeding`` |> getTerm props.nutritionContext.Label
            | NutritionCategory.EnteralSupplement ->
                Terms.``Nutrition Enteral Supplement`` |> getTerm props.nutritionContext.Label
            | NutritionCategory.TPN -> Terms.``Nutrition TPN`` |> getTerm props.nutritionContext.Label
            | NutritionCategory.Lipid -> Terms.``Nutrition Lipids`` |> getTerm props.nutritionContext.Label
            | NutritionCategory.ElectrolyteGlucose ->
                Terms.``Nutrition Electrolytes Glucose`` |> getTerm props.nutritionContext.Label

        // Use a ref for the plan so that closures captured by useElmish
        // always read the latest plan, even when useElmish doesn't re-initialize
        // (its deps only include ctx, not the plan).
        let planRef = React.useRef props.plan
        planRef.current <- props.plan

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let fixPrecision = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision

        let getWarning = ViewHelpers.getWarning

        let genericChange s =
            ctx
            |> OrderContext.medicationChange s
            |> fun updCtx ->
                // In Nutrition, Generic is upstream of Indication.
                // Clear Indication selection (but NOT the Indications list — server repopulates it).
                { updCtx with OrderContext.Filter.Indication = None }
            |> fun updCtx -> Api.PlanCommand.Navigate(planRef.current, Some ncId, Api.UpdateOrderContext, updCtx)
            |> props.planCommand

        let indicationChange s =
            ctx
            |> OrderContext.indicationChange s
            |> fun updCtx -> Api.PlanCommand.Navigate(planRef.current, Some ncId, Api.UpdateOrderContext, updCtx)
            |> props.planCommand

        let doseTypeChange s =
            let dt = s |> Option.map DoseType.doseTypeFromString

            ctx
            |> OrderContext.doseTypeChange dt
            |> fun updCtx -> Api.PlanCommand.Navigate(planRef.current, Some ncId, Api.UpdateOrderContext, updCtx)
            |> props.planCommand

        let updateOrderScenario (ol: OrderLoader) =
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
            |> fun updCtx ->
                Api.PlanCommand.Navigate(planRef.current, Some ncId, Api.UpdateOrderScenario, updCtx)
                |> props.planCommand

        let resetOrderScenario (_ol: OrderLoader) =
            Api.PlanCommand.Navigate(planRef.current, Some ncId, Api.ResetOrderScenario, ctx)
            |> props.planCommand

        let stepper =
            let create nav =
                fun (ol: OrderLoader) ->
                    let updCtx =
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

                    nav updCtx

            let createWithN nav =
                fun (n, uc) (ol: OrderLoader) ->
                    let updCtx =
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

                    nav (updCtx, n, uc)

            let createWithCmp nav =
                fun (ol: OrderLoader) ->
                    match ol.Component with
                    | None -> ()
                    | Some cmp ->
                        let updCtx =
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

                        nav (updCtx, cmp)

            let createWithCmpN nav =
                fun (n, uc) (ol: OrderLoader) ->
                    match ol.Component with
                    | None -> ()
                    | Some cmp ->
                        let updCtx =
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

                        nav (updCtx, cmp, n, uc)

            let navRate cmd =
                fun updCtx ->
                    Api.PlanCommand.Navigate(planRef.current, Some ncId, cmd, updCtx)
                    |> props.planCommand

            let navRateN cmd =
                fun (updCtx, n, uc) ->
                    Api.PlanCommand.Navigate(planRef.current, Some ncId, cmd (n, uc), updCtx)
                    |> props.planCommand

            let navCmpQty cmd =
                fun (updCtx, cmp) ->
                    Api.PlanCommand.Navigate(planRef.current, Some ncId, cmd cmp, updCtx)
                    |> props.planCommand

            let navCmpQtyN cmd =
                fun (updCtx, cmp, n, uc) ->
                    Api.PlanCommand.Navigate(planRef.current, Some ncId, cmd (cmp, n, uc), updCtx)
                    |> props.planCommand

            {|
                // Dose Rate
                setRateMin = create (navRate Api.SetMinOrderableDoseRateProperty)
                setRateDec = createWithN (navRateN Api.DecreaseOrderableDoseRateProperty)
                setRateMed = create (navRate Api.SetMedianOrderableDoseRateProperty)
                setRateInc = createWithN (navRateN Api.IncreaseOrderableDoseRateProperty)
                setRateMax = create (navRate Api.SetMaxOrderableDoseRateProperty)
                // Dose Quantity
                setDoseQtyMin = create (navRate Api.SetMinOrderableDoseQuantityProperty)
                setDoseQtyDec = createWithN (navRateN Api.DecreaseOrderableDoseQuantityProperty)
                setDoseQtyMed = create (navRate Api.SetMedianOrderableDoseQuantityProperty)
                setDoseQtyInc = createWithN (navRateN Api.IncreaseOrderableDoseQuantityProperty)
                setDoseQtyMax = create (navRate Api.SetMaxOrderableDoseQuantityProperty)
                // Component Quantity
                setComponentQtyMin = createWithCmp (navCmpQty Api.SetMinComponentOrderableQuantityProperty)
                setComponentQtyDec = createWithCmpN (navCmpQtyN Api.DecreaseComponentOrderableQuantityProperty)
                setComponentQtyMed = createWithCmp (navCmpQty Api.SetMedianComponentOrderableQuantityProperty)
                setComponentQtyInc = createWithCmpN (navCmpQtyN Api.IncreaseComponentOrderableQuantityProperty)
                setComponentQtyMax = createWithCmp (navCmpQty Api.SetMaxComponentOrderableQuantityProperty)
            |}

        let state, dispatch =
            React.useElmish (init (Resolved ctx), update updateOrderScenario resetOrderScenario stepper, [| box ctx |])

        let isOrderLoading = props.isRecalculating
        let isLoading = state.Order.IsNone && not props.isRecalculating
        let select = ViewHelpers.orderSelect true isOrderLoading
        let filterSelect = ViewHelpers.filterSelect isOrderLoading isOrderLoading
        let autoComplete = ViewHelpers.autoComplete isOrderLoading isOrderLoading
        let loadingIndicator = ViewHelpers.inlineProgress isOrderLoading

        // Use local state order when available, otherwise fall back to the
        // order carried by the parent context so that the UI stays populated
        // while the server is processing.
        let displayOrder =
            state.Order
            |> Option.orElseWith (fun () -> ctx.Scenarios |> Array.tryExactlyOne |> Option.map _.Order)

        let componentRows =
            match displayOrder with
            | Some ord ->
                ord.Orderable.Components
                |> Array.map (fun cmp ->
                    // Quantity control (bereiding)
                    let qtyVals = cmp.OrderableQuantity |> ViewHelpers.ovarValsWithRange string 3

                    // can jump to the min, median, or max
                    let navigable = cmp.OrderableQuantity |> OrderVariable.isNavigable
                    let solved = ord |> isSolved

                    let nav =
                        let c = qtyVals |> Array.length

                        let show =
                            cmp.OrderableQuantity.Variable.Min.IsSome
                            && cmp.OrderableQuantity.Variable.Incr.IsSome
                            && cmp.OrderableQuantity.Variable.Max.IsSome
                            || c >= 1

                        if not show then
                            None
                        else
                            let cmpName = cmp.Name

                            ViewHelpers.createStepper
                                dispatch
                                revision
                                navigable
                                solved
                                (SetMinComponentQuantityProperty cmpName)
                                (fun (n, uc) -> DecreaseComponentQuantityProperty(cmpName, n, uc))
                                (SetMedianComponentQuantityProperty cmpName)
                                (fun (n, uc) -> IncreaseComponentQuantityProperty(cmpName, n, uc))
                                (SetMaxComponentQuantityProperty cmpName)
                                (cmp.OrderableQuantity |> ViewHelpers.ovarStep string)

                    let qtyWarning = cmp.OrderableQuantity.Level |> getWarning

                    let qtyLabel = cmp.OrderableQuantity |> ViewHelpers.ovarLabel cmp.Name

                    let qtyControl =
                        select
                            isLoading
                            qtyLabel
                            None
                            (fun s -> ChangeComponentOrderableQuantity(cmp.Name, s) |> dispatch)
                            nav
                            false
                            qtyWarning
                            (Some 400)
                            qtyVals

                    // Dose display (dosering) - always show with label
                    let doseLabel = cmp.Dose.QuantityAdjust |> ViewHelpers.ovarLabel cmp.Name
                    let doseWarning = cmp.Dose.QuantityAdjust.Level |> getWarning
                    let doseVals = cmp.Dose.QuantityAdjust |> ViewHelpers.ovarVals (fixPrecision 3)

                    let doseDisplay =
                        select
                            isLoading
                            doseLabel
                            None
                            (fun s -> ChangeComponentDoseQuantityAdjust(cmp.Name, s) |> dispatch)
                            None
                            false
                            doseWarning
                            (Some 400)
                            doseVals

                    JSX.jsx
                        $"""
                    import Grid from '@mui/material/Grid';
                    import Box from '@mui/material/Box';
                    <Grid container spacing={{2}} sx={flexEndSx}>
                        <Grid size={halfSize}>
                            <Box sx={cellSx}>
                                {qtyControl}
                            </Box>
                        </Grid>
                        <Grid size={halfSize}>
                            <Box sx={cellSx}>
                                {doseDisplay}
                            </Box>
                        </Grid>
                    </Grid>
                    """
                )
            | None -> [| null |]

        let isEnteral =
            props.nutritionContext.Category = NutritionCategory.EnteralFeeding
            || props.nutritionContext.Category = NutritionCategory.EnteralSupplement

        let selectMinWidth = if isEnteral then None else Some 400

        let doseQtyControl =
            match displayOrder with
            | Some ord ->
                let warning = ord.Orderable.Dose.Quantity.Level |> getWarning

                let label =
                    ord.Orderable.Dose.Quantity |> ViewHelpers.ovarLabel "toedien hoeveelheid"

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
                    isLoading
                    label
                    None
                    (ChangeOrderableDoseQuantity >> dispatch)
                    doseQtyNav
                    false
                    warning
                    selectMinWidth
                    vals
            | None -> null

        let dosePerTimeAdjDisplay =
            match displayOrder with
            | Some ord ->
                ord.Orderable.Dose.PerTimeAdjust
                |> ViewHelpers.ovarDisplay select "dosering" (fixPrecision 3) selectMinWidth
            | None -> null

        let frequencyControl =
            match displayOrder with
            | Some ord when ord.Schedule.IsDiscontinuous || ord.Schedule.IsTimed ->
                let warning = ord.Schedule.Frequency.Level |> getWarning
                let label = ord.Schedule.Frequency |> ViewHelpers.ovarLabel "frequentie"
                let freqVals = ord.Schedule.Frequency |> ViewHelpers.ovarVals string

                select isLoading label None (ChangeFrequency >> dispatch) None false warning selectMinWidth freqVals
            | _ -> null

        let genericFilter =
            let sel = ctx.Filter.Generic
            let items = ctx.Filter.Generics
            let lbl = Terms.Composition |> getTerm "Samenstelling"
            let onChange = genericChange

            if isMobile then
                items |> Array.map (fun s -> s, s) |> filterSelect lbl sel onChange
            else
                items |> autoComplete lbl sel onChange

        let frequencyDoseRow =
            if isEnteral then
                let flexSx =
                    {|
                        display = "flex"
                        flexWrap = "wrap"
                        gap = 4
                        alignItems = "flex-end"
                        width = "100%"
                    |}

                let itemSx =
                    {|
                        flex = "1 1 0%"
                        minWidth = 200
                        ``& .MuiFormControl-root`` = {| width = "100%" |}
                        ``& .MuiAutocomplete-root`` = {| minWidth = "unset" |}
                    |}

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                <Box sx={flexSx}>
                    <Box sx={itemSx}>
                        {genericFilter}
                    </Box>                        
                    <Box sx={itemSx}>
                        {frequencyControl}
                    </Box>
                    <Box sx={itemSx}>
                        {doseQtyControl}
                    </Box>
                    <Box sx={itemSx}>
                        {dosePerTimeAdjDisplay}
                    </Box>
                </Box>
                """
            else
                JSX.jsx
                    $"""
                import Grid from '@mui/material/Grid';
                import Box from '@mui/material/Box';
                <Grid container spacing={{2}} sx={flexEndSx}>
                    <Grid size={halfSize}>
                        <Box sx={cellSx}>
                            {frequencyControl}
                        </Box>
                    </Grid>
                    <Grid size={halfSize}>
                        <Box sx={cellSx}>
                            {doseQtyControl}
                        </Box>
                    </Grid>
                </Grid>
                """

        let rateControl =
            match displayOrder with
            | Some ord when ord.Schedule.IsTimed || ord.Schedule.IsContinuous ->
                let solved = ord |> isSolved
                // can jump to the min, median, or max
                let navigable = ord.Orderable.Dose.Rate |> OrderVariable.isNavigable

                let nav =
                    ViewHelpers.createStepper
                        dispatch
                        revision
                        navigable
                        solved
                        SetMinDoseRateProperty
                        DecreaseDoseRateProperty
                        SetMedianDoseRateProperty
                        IncreaseDoseRateProperty
                        SetMaxDoseRateProperty
                        (ord.Orderable.Dose.Rate |> ViewHelpers.ovarStep string)

                let warning = ord.Orderable.Dose.Rate.Level |> getWarning
                let label = ord.Orderable.Dose.Rate |> ViewHelpers.ovarLabel "infuussnelheid"

                let rateDisplay =
                    ord.Orderable.Dose.Rate
                    |> ViewHelpers.ovarValsWithRange string 3
                    |> select isLoading label None (ChangeOrderableDoseRate >> dispatch) nav false warning (Some 400)

                let timeDisplay =
                    ord.Schedule.Time
                    |> ViewHelpers.ovarDisplay select "looptijd" (fixPrecision 3) (Some 400)

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
                |> ViewHelpers.ovarDisplay select "totaal volume" string (Some 400)
            | None -> null

        let onClickReset = fun () -> ResetOrderScenario |> dispatch

        let administrationDivider =
            JSX.jsx $"""<Divider><Typography variant="caption">toediening</Typography></Divider>"""

        let headerRow =
            JSX.jsx
                $"""
            import Grid from '@mui/material/Grid';
            import Typography from '@mui/material/Typography';
            import Divider from '@mui/material/Divider';
            <Grid container spacing={{2}}>
                <Grid size={halfSize}>
                    <Divider><Typography variant="caption">bereiding</Typography></Divider>
                </Grid>
                <Grid size={halfSize}>
                    <Divider><Typography variant="caption">dosering</Typography></Divider>
                </Grid>
            </Grid>
            """

        let preparationSection =
            if isEnteral then
                null
            else
                JSX.jsx
                    $"""
                import Divider from '@mui/material/Divider';
                <>
                    {headerRow}
                    {componentRows |> unbox<seq<ReactElement>> |> React.Fragment}
                    <Divider />
                    {totalVolumeDisplay}
                </>
                """

        let details =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            import Divider from '@mui/material/Divider';
            import Typography from '@mui/material/Typography';
            import Button from '@mui/material/Button';
            <Stack direction={"column"} spacing={1} >
                {preparationSection}
                {if isEnteral then null else administrationDivider}
                {frequencyDoseRow}
                {rateControl}
                {loadingIndicator}
                <Button
                    variant="outlined"
                    size="small"
                    disabled={isOrderLoading}
                    onClick={fun _ -> onClickReset ()}
                >
                    Reset
                </Button>
            </Stack>
            """

        let indicationFilter =
            if ctx.Filter.Generic.IsNone || ctx.Filter.Indications |> Array.length <= 1 then
                null
            else
                let sel = ctx.Filter.Indication
                let items = ctx.Filter.Indications
                let lbl = Terms.Indication |> getTerm "Indicatie"

                if isMobile then
                    items |> Array.map (fun s -> s, s) |> filterSelect lbl sel indicationChange
                else
                    items |> autoComplete lbl sel indicationChange

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

        let filterSx = {| marginBottom = 2 |}

        let filterControls =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            <Stack direction="column" spacing={2} sx={filterSx}>
                {if isEnteral && displayOrder.IsSome then
                     null
                 else
                     genericFilter}
                {indicationFilter}
                {doseTypeFilter}
            </Stack>
            """

        let orderDetails = if displayOrder.IsSome then details else loadingIndicator

        let expanded, setExpanded = React.useState true

        let handleAccordionChange = fun _ -> setExpanded (not expanded)

        let removeButton =
            match props.onRemove with
            | Some onRemove ->
                let handleClick (e: Browser.Types.Event) =
                    e.stopPropagation ()
                    onRemove ()

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import DeleteIcon from '@mui/icons-material/Delete';
                <Box
                    component="span"
                    onClick={handleClick}
                    sx={removeButtonSx}
                >
                    <DeleteIcon fontSize="small" />
                </Box>
                """
            | None -> null

        if props.wrapInAccordion then
            let summary =
                let adminSummary =
                    match ctx.Scenarios with
                    | [| sc |] when sc.Administration |> Array.isEmpty |> not ->
                        let blocks = sc.Administration |> TextBlock.flatten |> Array.collect id
                        renderAdminSummary (string props.nutritionContext.Id) sc.Order.Orderable.Name blocks
                    | _ -> null

                JSX.jsx
                    $"""
                import React from 'react';
                import Typography from '@mui/material/Typography';
                import Box from '@mui/material/Box';

                <Box sx={flexOverflowSx}>
                    <Typography>{props.nutritionContext.Label}</Typography>
                    {adminSummary}
                    <Box sx={autoMarginSx}>
                        {removeButton}
                    </Box>
                </Box>
                """

            let children =
                JSX.jsx
                    $"""
                import React from 'react';

                <React.Fragment>
                    {filterControls}
                    {orderDetails}
                </React.Fragment>
                """

            Components.Accordion.View
                {|
                    expanded = expanded
                    onChange = fun () -> setExpanded (not expanded)
                    summary = summary
                    children = children
                    isMobile = isMobile
                    detailsPaddingTop = if isMobile then None else Some 4
                    ariaControls = None
                    summaryId = None
                |}
        else
            JSX.jsx
                $"""
            import React from "react";
            import Stack from '@mui/material/Stack';
            import Divider from '@mui/material/Divider';
            import Typography from '@mui/material/Typography';
            import Box from '@mui/material/Box';

            <Box>
                <Divider>
                    <Stack direction="row" spacing={{1}} sx={alignCenterSx}>
                        <Typography variant="caption">{props.nutritionContext.Label}</Typography>
                        {removeButton}
                    </Stack>
                </Divider>
                {filterControls}
                {orderDetails}
            </Box>
            """


    [<JSX.Component>]
    let private AddButton
        (props:
            {|
                label: string
                onClick: unit -> unit
            |})
        =
        JSX.jsx
            $"""
        import Button from '@mui/material/Button';
        import AddIcon from '@mui/icons-material/Add';
        <Button
            variant="outlined"
            size="small"
            startIcon={{ <AddIcon /> }}
            onClick={fun _ -> props.onClick ()}
            sx={addButtonSx}
        >
            {props.label}
        </Button>
        """


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let patient = (AppEnv.asEnv<AppEnv.IPatient> props.appEnv).Patient
        // the one plan: the nutrition workbenches live in the order plan, which is there
        // with the patient
        let envOrderPlan = AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv
        let orderPlan = envOrderPlan.OrderPlan
        let planCommand = envOrderPlan.PlanCommand

        let localizationTerms =
            (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let progress =
            match orderPlan with
            | HasNotStartedYet when patient.IsNone ->
                let msg =
                    Terms.``Patient enter patient data`` |> getTerm "Voer patient gegevens in ..."

                JSX.jsx $"<>{msg}</>"
            | _ -> ViewHelpers.progressOrEmpty orderPlan

        let isRecalculating =
            match orderPlan with
            | Recalculating _ -> true
            | _ -> false

        let confirmDeleteTarget, setConfirmDeleteTarget = React.useState<string option> None
        let enteralExpanded, setEnteralExpanded = React.useState true
        let printOpen, setPrintOpen = React.useState false

        let makeSlot wrapInAccordion (plan: OrderPlan) nc =
            let onRemove =
                if nc.Removable then
                    let hasSupplements =
                        nc.Category = NutritionCategory.EnteralFeeding
                        && plan.NutritionContexts
                           |> Array.exists (fun c -> c.Category = NutritionCategory.EnteralSupplement)

                    Some(fun () ->
                        if hasSupplements then
                            setConfirmDeleteTarget (Some nc.Id)
                        else
                            Api.PlanCommand.RemoveContext(plan, nc.Id) |> planCommand
                    )
                else
                    None

            NutritionSlot
                {|
                    nutritionContext = nc
                    plan = plan
                    planCommand = planCommand
                    localizationTerms = localizationTerms
                    onRemove = onRemove
                    wrapInAccordion = wrapInAccordion
                    isRecalculating = isRecalculating
                |}

        let addContext (plan: OrderPlan) category =
            Api.PlanCommand.AddContext(plan, category) |> planCommand

        let hasCategory (plan: OrderPlan) cat =
            plan.NutritionContexts |> Array.exists (fun nc -> nc.Category = cat)

        let content =
            match orderPlan with
            | Resolved plan
            | Recalculating plan ->
                let enteralContexts =
                    plan.NutritionContexts
                    |> Array.filter (fun nc ->
                        nc.Category = NutritionCategory.EnteralFeeding
                        || nc.Category = NutritionCategory.EnteralSupplement
                    )

                let parenteralContexts =
                    plan.NutritionContexts
                    |> Array.filter (fun nc ->
                        nc.Category = NutritionCategory.TPN
                        || nc.Category = NutritionCategory.Lipid
                        || nc.Category = NutritionCategory.ElectrolyteGlucose
                    )

                let enteralSlots = enteralContexts |> Array.map (makeSlot false plan)

                let parenteralSlots = parenteralContexts |> Array.map (makeSlot true plan)

                let hasEnteral = hasCategory plan NutritionCategory.EnteralFeeding
                let hasTPN = hasCategory plan NutritionCategory.TPN
                let hasLipid = hasCategory plan NutritionCategory.Lipid

                let enteralFeedingAddButton =
                    if not hasEnteral then
                        AddButton
                            {|
                                label = Terms.``Nutrition Enteral Feeding`` |> getTerm "Enterale Voeding"
                                onClick = fun () -> addContext plan NutritionCategory.EnteralFeeding
                            |}
                    else
                        null

                let supplementAddButton =
                    if hasEnteral then
                        AddButton
                            {|
                                label = Terms.``Nutrition Add Supplement`` |> getTerm "Supplement toevoegen"
                                onClick = fun () -> addContext plan NutritionCategory.EnteralSupplement
                            |}
                    else
                        null

                let parenteralAddButtons =
                    [|
                        if not hasTPN then
                            AddButton
                                {|
                                    label = Terms.``Nutrition TPN`` |> getTerm "TPN"
                                    onClick = fun () -> addContext plan NutritionCategory.TPN
                                |}
                        if not hasLipid then
                            AddButton
                                {|
                                    label = Terms.``Nutrition Lipids`` |> getTerm "Vetten"
                                    onClick = fun () -> addContext plan NutritionCategory.Lipid
                                |}
                        AddButton
                            {|
                                label = Terms.``Nutrition Electrolytes Glucose`` |> getTerm "Elektrolyten/Glucose"
                                onClick = fun () -> addContext plan NutritionCategory.ElectrolyteGlucose
                            |}
                    |]

                let enteralAccordion =
                    let summary =
                        let adminSummaries =
                            enteralContexts
                            |> Array.choose (fun nc ->
                                match nc.OrderContext.Scenarios with
                                | [| sc |] when sc.Administration |> Array.isEmpty |> not ->
                                    let blocks = sc.Administration |> TextBlock.flatten |> Array.collect id
                                    renderAdminSummary (string nc.Id) sc.Order.Orderable.Name blocks |> Some
                                | _ -> None
                            )

                        JSX.jsx
                            $"""
                        import Typography from '@mui/material/Typography';
                        import Box from '@mui/material/Box';

                        <Box sx={flexOverflowSx}>
                            <Typography>{Terms.``Nutrition Enteral Feeding`` |> getTerm "Enterale Voeding"}</Typography>
                            {adminSummaries |> unbox<seq<ReactElement>> |> React.Fragment}
                        </Box>
                        """

                    let children =
                        JSX.jsx
                            $"""
                        import Stack from '@mui/material/Stack';
                        <Stack direction="column" spacing={{2}}>
                            {enteralSlots |> unbox<seq<ReactElement>> |> React.Fragment}
                            {enteralFeedingAddButton}
                            {supplementAddButton}
                        </Stack>
                        """

                    Components.Accordion.View
                        {|
                            expanded = enteralExpanded
                            onChange = fun () -> setEnteralExpanded (not enteralExpanded)
                            summary = summary
                            children = children
                            isMobile = isMobile
                            detailsPaddingTop = if isMobile then None else Some 4
                            ariaControls = None
                            summaryId = None
                        |}

                let printDisabled = parenteralContexts |> Array.isEmpty

                JSX.jsx
                    $"""
                import Stack from '@mui/material/Stack';
                import Typography from '@mui/material/Typography';
                import Divider from '@mui/material/Divider';
                import IconButton from '@mui/material/IconButton';
                import PrintIcon from '@mui/icons-material/Print';

                <Stack direction="column" spacing={1}>
                    {enteralAccordion}
                    <Divider sx={dividerSx} />
                    <Stack direction="row" sx={alignCenterSx} spacing={1}>
                        <Typography variant="h6">{Terms.``Nutrition Parenteral Section`` |> getTerm "Parenteraal"}</Typography>
                        <Button color="primary" size="small" disabled={printDisabled} onClick={fun _ -> setPrintOpen true} startIcon={{<PrintIcon />}}>
                            {Terms.Print |> getTerm "Print"}
                        </Button>
                    </Stack>
                    {parenteralSlots |> unbox<seq<ReactElement>> |> React.Fragment}
                    <Stack direction="row" spacing={1}>
                        {parenteralAddButtons |> unbox<seq<ReactElement>> |> React.Fragment}
                    </Stack>
                </Stack>
                """
            | _ -> null

        let confirmDeleteDialog =
            let isOpen = confirmDeleteTarget.IsSome
            let handleCancel = fun _ -> setConfirmDeleteTarget None

            let handleConfirm =
                fun _ ->
                    match confirmDeleteTarget, orderPlan with
                    | Some ncId, (Resolved plan | Recalculating plan) ->
                        Api.PlanCommand.RemoveContext(plan, ncId) |> planCommand
                    | _ -> ()

                    setConfirmDeleteTarget None

            JSX.jsx
                $"""
            import Dialog from '@mui/material/Dialog';
            import DialogTitle from '@mui/material/DialogTitle';
            import DialogContent from '@mui/material/DialogContent';
            import DialogContentText from '@mui/material/DialogContentText';
            import DialogActions from '@mui/material/DialogActions';
            import Button from '@mui/material/Button';

            <Dialog open={isOpen} onClose={handleCancel}>
                <DialogTitle>{Terms.``Nutrition Remove Enteral Title``
                              |> getTerm "Enterale voeding verwijderen"}</DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        {Terms.``Nutrition Remove Enteral Text``
                         |> getTerm
                             "Als u de enterale voeding verwijdert, worden ook alle bijbehorende supplementen verwijderd. Wilt u doorgaan?"}
                    </DialogContentText>
                </DialogContent>
                <DialogActions>
                    <Button onClick={handleCancel}>{Terms.Cancel |> getTerm "Annuleren"}</Button>
                    <Button onClick={handleConfirm} color="error">{Terms.Delete |> getTerm "Verwijderen"}</Button>
                </DialogActions>
            </Dialog>
            """

        let printDialog =
            match printOpen, orderPlan with
            | true, (Resolved plan | Recalculating plan) ->
                ParenteralPrintView
                    {|
                        plan = plan
                        onClose = fun () -> setPrintOpen false
                    |}
            | _ -> null

        JSX.jsx
            $"""
        import React from "react";
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Box>
            {content}
            {progress}
            {confirmDeleteDialog}
            {printDialog}
        </Box>
        """
