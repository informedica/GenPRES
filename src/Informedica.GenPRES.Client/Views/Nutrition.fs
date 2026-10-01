namespace Views

#nowarn "1104"

module Nutrition =

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
    open NutritionSlot


    let private boldCellSx = {| fontWeight = "bold" |}

    let private autoMarginSx = {| marginLeft = "auto" |}

    // the summary wraps onto more lines rather than clip a value at the header's edge
    let private flexOverflowSx =
        {|
            display = "flex"
            flexWrap = "wrap"
            alignItems = "center"
            width = "100%"
        |}

    let private dividerSx =
        {|
            marginTop = 2
            marginBottom = 2
        |}


    // the administration of a scenario as pills, one per value the server printed, each in
    // its own severity
    let private adminSummary (name: string) (rows: TextBlock[][]) =
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


    [<JSX.Component>]
    let private ParenteralPrintView
        (props:
            {|
                plan: OrderPlan
                onClose: unit -> unit
            |})
        =
        let printLabels = Global.printLabels ()
        let weightKg = ViewHelpers.PrintView.patientWeight printLabels.unknown (props.plan.Patient |> Some)

        let parenteralContexts = props.plan.OrderContexts |> Array.filter (isOneOf parenteral)

        let tableSx =
            {|
                tableLayout = "fixed"
                width = "100%"
            |}

        let contextSections =
            parenteralContexts
            |> Array.map (fun nc ->
                let scenario = nc.Scenarios |> Array.tryExactlyOne
                let label = OrderContext.label nc

                match scenario with
                | None ->
                    let mb2Sx = {| marginBottom = 2 |}

                    let header =
                        ViewHelpers.PrintView.SectionHeader
                            {|
                                label = label
                                marginBottom = 0
                            |}

                    JSX.jsx
                        $"""
                    import Box from '@mui/material/Box';
                    import Typography from '@mui/material/Typography';

                    <Box key={nc.Id} sx={mb2Sx}>
                        {header}
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

                            let doseAdj = cmp.Dose.QuantityAdjust |> OrderVariable.displayStringFormatted fixPrec2

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

                    let header =
                        ViewHelpers.PrintView.SectionHeader
                            {|
                                label = $"%s{label} - %s{orderableName}"
                                marginBottom = 1
                            |}

                    JSX.jsx
                        $"""
                    import Box from '@mui/material/Box';
                    import Table from '@mui/material/Table';
                    import TableBody from '@mui/material/TableBody';
                    import TableRow from '@mui/material/TableRow';
                    import TableCell from '@mui/material/TableCell';
                    import TableHead from '@mui/material/TableHead';

                    <Box key={nc.Id} sx={ {| marginBottom = 3 |} }>
                        {header}
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
                            |> Array.map ViewHelpers.textItemText
                            |> String.concat " "
                        else
                            ""

                    let normal =
                        if items.Length >= 1 then
                            let s = items[items.Length - 1] |> ViewHelpers.textItemText

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
                let header =
                    ViewHelpers.PrintView.SectionHeader
                        {|
                            label = "Totalen"
                            marginBottom = 1
                        |}

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import Table from '@mui/material/Table';
                import TableBody from '@mui/material/TableBody';

                <Box sx={ {| marginTop = 2 |} }>
                    {header}
                    <Table size="small" sx={tableSx}>
                        <TableBody>
                            {totalsRows |> unbox<seq<ReactElement>> |> React.Fragment}
                        </TableBody>
                    </Table>
                </Box>
                """

        let printContent =
            let header =
                ViewHelpers.PrintView.SectionHeader
                    {|
                        label = "INFUUS AFSPRAKEN CENTRAAL VENEUZE CATHETERS"
                        marginBottom = 2
                    |}

            JSX.jsx
                $"""
            <React.Fragment>
                {header}
                {ViewHelpers.PrintView.PatientHeader
                     {|
                         weightKg = weightKg
                         labels = printLabels
                     |}}
                {contextSections |> unbox<seq<ReactElement>> |> React.Fragment}
                {totalsSection}
                {ViewHelpers.PrintView.PatientSignature {| label = printLabels.signature |}}
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
    let private NutritionSlotView
        (props:
            {|
                nutritionContext: OrderContext
                plan: OrderPlan
                planCommand: Api.OrderPlanCommand -> unit
                // a clear from a field's arrow, and the list of such a reopen closed without a pick
                planReopen: Api.OrderPlanCommand -> unit
                planRestore: unit -> unit
                localizationTerms: Deferred<string[][]>
                onRemove: (unit -> unit) option
                wrapInAccordion: bool
                isRecalculating: bool
            |})
        =
        let ctx = props.nutritionContext
        let isEnteral = ctx |> isOneOf enteral

        let slot =
            useSlot
                {|
                    nutritionContext = ctx
                    plan = props.plan
                    planCommand = props.planCommand
                    planReopen = props.planReopen
                    planRestore = props.planRestore
                    localizationTerms = props.localizationTerms
                    isRecalculating = props.isRecalculating
                    fieldMinWidth = if isEnteral then None else Some 400
                |}

        let label = slot.Label
        let isMobile = slot.IsMobile
        let isOrderLoading = slot.IsLoading
        let displayOrder = slot.Order
        let genericFilter = slot.GenericFilter
        let indicationFilter = slot.IndicationFilter
        let doseTypeFilter = slot.DoseTypeFilter
        let frequencyControl = slot.FrequencyControl
        let doseQtyControl = slot.DoseQuantityControl
        let dosePerTimeAdjDisplay = slot.DosePerTimeAdjustDisplay
        let rateControl = slot.RateControl
        let totalVolumeDisplay = slot.TotalVolumeDisplay
        let headerRow = slot.ComponentHeader
        let componentRows = slot.ComponentRows
        let administrationDivider = slot.AdministrationHeading
        let resetBar = slot.ResetBar
        let loadingIndicator = slot.LoadingIndicator

        let frequencyDoseRow =
            if isEnteral then
                // The row is measured, not the window: the container query below decides, and a
                // field that never gets buttons here drops its hidden slots.
                let containerSx =
                    {| Components.QuantityField.dropUnusedSlotsSx with
                        containerType = "inline-size"
                        width = "100%"
                    |}

                // Every feeding keeps its items on one row while they fit: the filter with the
                // room of a severity mark, frequency with its inner buttons, the dose field with
                // all its buttons, dose per time without, and the gaps. Narrower, all items stand
                // in one column, never wrapped part-way and never scrolled sideways.
                let rowWidth =
                    200
                    + Components.QuantityField.markWidth
                    + Components.QuantityField.fieldWidthInnerSlots
                    + Components.QuantityField.fieldWidth
                    + Components.QuantityField.fieldWidthWithoutSlots
                    + 3 * 16

                let flexSx =
                    createObj
                        [
                            "display" ==> "flex"
                            "flexWrap" ==> "nowrap"
                            "gap" ==> 2
                            "alignItems" ==> "flex-end"
                            $"@container (max-width: %i{rowWidth - 1}px)"
                            ==> {|
                                    flexDirection = "column"
                                    alignItems = "stretch"
                                |}
                        ]

                // the filter keeps the room a field gives its severity mark, so the gaps between
                // all items look the same
                let itemSx =
                    {|
                        flex = "1 1 0%"
                        minWidth = 200
                        paddingRight = $"%i{Components.QuantityField.markWidth}px"
                        ``& .MuiFormControl-root`` = {| width = "100%" |}
                        ``& .MuiAutocomplete-root`` = {| minWidth = "unset" |}
                    |}

                // a quantity field is as wide as its buttons and a readable value need
                let fieldItemSx =
                    {|
                        flex = "1 1 0%"
                        minWidth = "min-content"
                    |}

                // frequency keeps one width whether it is chosen from the dropdown or stepped
                let frequencyItemSx = {| fieldItemSx with minWidth = Components.QuantityField.fieldWidthInnerSlots |}

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                <Box sx={containerSx}>
                    <Box sx={flexSx}>
                        <Box sx={itemSx}>
                            {genericFilter}
                        </Box>
                        <Box sx={frequencyItemSx}>
                            {frequencyControl}
                        </Box>
                        <Box sx={fieldItemSx}>
                            {doseQtyControl}
                        </Box>
                        <Box sx={fieldItemSx}>
                            {dosePerTimeAdjDisplay}
                        </Box>
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
            <Stack direction={"column"} spacing={1} >
                {preparationSection}
                {if isEnteral then null else administrationDivider}
                {frequencyDoseRow}
                {rateControl}
                {resetBar}
            </Stack>
            """
            |> fun fields ->
                // the spinner lies over the details and takes no room, so the fields stay where
                // they are while the order reloads
                Components.LoadingOverlay.View
                    {|
                        isLoading = isOrderLoading
                        children = fields
                    |}

        let filterSx = {| marginBottom = 2 |}

        // a parenteral slot shows the composition and the dose type side by side
        let compositionRow =
            if isEnteral || isNull doseTypeFilter then
                null
            else
                JSX.jsx
                    $"""
                import Grid from '@mui/material/Grid';
                <Grid container spacing={{2}}>
                    <Grid size={halfSize}>
                        {genericFilter}
                    </Grid>
                    <Grid size={halfSize}>
                        {doseTypeFilter}
                    </Grid>
                </Grid>
                """

        let filterControls =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            <Stack direction="column" spacing={2} sx={filterSx}>
                {if not (isNull compositionRow) then compositionRow
                 elif isEnteral && displayOrder.IsSome then null
                 else genericFilter}
                {indicationFilter}
                {if isNull compositionRow then doseTypeFilter else null}
            </Stack>
            """

        let orderDetails = if displayOrder.IsSome then details else loadingIndicator

        let expanded, setExpanded = React.useState true

        let handleAccordionChange = fun _ -> setExpanded (not expanded)

        let removeButton =
            match props.onRemove with
            | Some onRemove ->
                // the plan takes one change at a time: no removal while one is under way
                Components.RemoveIconButton.View
                    {|
                        onRemove = onRemove
                        busy = props.isRecalculating
                    |}
            | None -> null

        if props.wrapInAccordion then
            let summary =
                let adminSummary =
                    match ctx.Scenarios with
                    | [| sc |] when sc.Administration |> Array.isEmpty |> not ->
                        adminSummary sc.Order.Orderable.Name sc.Administration
                    | _ -> null

                JSX.jsx
                    $"""
                import React from 'react';
                import Typography from '@mui/material/Typography';
                import Box from '@mui/material/Box';

                <Box sx={flexOverflowSx}>
                    <Typography>{label}</Typography>
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

            Components.Disclosure.View
                {|
                    isOpen = expanded
                    onToggle = fun () -> setExpanded (not expanded)
                    summary = summary
                    children = children
                    isMobile = isMobile
                    detailsPaddingTop = if isMobile then None else Some 4
                    ariaControls = None
                    summaryId = None
                |}
        else
            // the section's name on its line, with the button that removes the section beside it
            let heading =
                Components.SectionHeading.View
                    {|
                        label = label
                        action = Some removeButton
                    |}

            JSX.jsx
                $"""
            import React from "react";
            import Box from '@mui/material/Box';

            <Box>
                {heading}
                {filterControls}
                {orderDetails}
            </Box>
            """


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        // the one plan: the nutrition workbenches live in the order plan, which is there
        // with the patient
        let envOrderPlan = AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv
        let orderPlan = envOrderPlan.OrderPlan
        let planCommand = envOrderPlan.OrderPlanCommand

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        // no patient: the notice says what is missing. A patient whose plan is being opened
        // shows as Changing, with the sections' own overlay and greyed slots, so no spinner here
        let patientNotice =
            Components.PatientNotice.View
                {|
                    appEnv = props.appEnv
                    needs = PatientReadiness.Needs.Calculation
                |}

        // the slots and the buttons rest while a change is under way: the plan takes one change
        // at a time
        let isRecalculating =
            match orderPlan with
            | OrderPlanView.Changing _ -> true
            | OrderPlanView.NoPatient
            | OrderPlanView.Settled _ -> false

        let confirmDeleteTarget, setConfirmDeleteTarget = React.useState<string option> None
        let enteralExpanded, setEnteralExpanded = React.useState true
        let printOpen, setPrintOpen = React.useState false

        let makeSlot wrapInAccordion (plan: OrderPlan) (nc: OrderContext) =
            let onRemove =
                let hasSupplements =
                    nc |> isOneOf [ NutritionCategory.EnteralFeeding ]
                    && plan.OrderContexts
                       |> Array.exists (isOneOf [ NutritionCategory.EnteralSupplement ])

                Some(fun () ->
                    if hasSupplements then
                        setConfirmDeleteTarget (Some nc.Id)
                    else
                        Api.OrderPlanCommand.RemoveOrderContexts(plan, [| nc.Id |]) |> planCommand
                )

            NutritionSlotView
                {|
                    nutritionContext = nc
                    plan = plan
                    planCommand = planCommand
                    planReopen = envOrderPlan.Reopen
                    planRestore = envOrderPlan.Restore
                    localizationTerms = localizationTerms
                    onRemove = onRemove
                    wrapInAccordion = wrapInAccordion
                    isRecalculating = isRecalculating
                |}

        let newOrderContext (plan: OrderPlan) category =
            Api.OrderPlanCommand.NewOrderContext(plan, category) |> planCommand

        let content =
            match orderPlan with
            | OrderPlanView.Settled(plan, _)
            | OrderPlanView.Changing(plan, _) ->
                let enteralContexts = plan.OrderContexts |> Array.filter (isOneOf enteral)
                let parenteralContexts = plan.OrderContexts |> Array.filter (isOneOf parenteral)

                let enteralSlots = enteralContexts |> Array.map (makeSlot false plan)

                let parenteralSlots = parenteralContexts |> Array.map (makeSlot true plan)

                // the buttons the plan admits a context for, as the server would rule
                let mayAdd category = plan |> OrderPlan.mayAdd category

                let enteralFeedingAddButton =
                    if mayAdd NutritionCategory.EnteralFeeding then
                        Components.AddButton.View
                            {|
                                label = Terms.``Nutrition Enteral Feeding`` |> getTerm "Enterale Voeding"
                                onClick = fun () -> newOrderContext plan NutritionCategory.EnteralFeeding
                                disabled = isRecalculating
                            |}
                    else
                        null

                let supplementAddButton =
                    if mayAdd NutritionCategory.EnteralSupplement then
                        Components.AddButton.View
                            {|
                                label = Terms.``Nutrition Add Supplement`` |> getTerm "Supplement toevoegen"
                                onClick = fun () -> newOrderContext plan NutritionCategory.EnteralSupplement
                                disabled = isRecalculating
                            |}
                    else
                        null

                let parenteralAddButtons =
                    [|
                        if mayAdd NutritionCategory.TPN then
                            Components.AddButton.View
                                {|
                                    label = Terms.``Nutrition TPN`` |> getTerm "TPN"
                                    onClick = fun () -> newOrderContext plan NutritionCategory.TPN
                                    disabled = isRecalculating
                                |}
                        if mayAdd NutritionCategory.Lipid then
                            Components.AddButton.View
                                {|
                                    label = Terms.``Nutrition Lipids`` |> getTerm "Vetten"
                                    onClick = fun () -> newOrderContext plan NutritionCategory.Lipid
                                    disabled = isRecalculating
                                |}
                        if mayAdd NutritionCategory.ElectrolyteGlucose then
                            Components.AddButton.View
                                {|
                                    label = Terms.``Nutrition Electrolytes Glucose`` |> getTerm "Elektrolyten/Glucose"
                                    onClick = fun () -> newOrderContext plan NutritionCategory.ElectrolyteGlucose
                                    disabled = isRecalculating
                                |}
                    |]

                let enteralAccordion =
                    let summary =
                        let adminSummaries =
                            enteralContexts
                            |> Array.choose (fun nc ->
                                match nc.Scenarios with
                                | [| sc |] when sc.Administration |> Array.isEmpty |> not ->
                                    (string nc.Id, adminSummary sc.Order.Orderable.Name sc.Administration) |> Some
                                | _ -> None
                            )
                            |> withKey

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

                    Components.Disclosure.View
                        {|
                            isOpen = enteralExpanded
                            onToggle = fun () -> setEnteralExpanded (not enteralExpanded)
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
                import Button from '@mui/material/Button';
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
            | OrderPlanView.NoPatient -> null

        let confirmDeleteDialog =
            let isOpen = confirmDeleteTarget.IsSome
            let handleCancel = fun () -> setConfirmDeleteTarget None

            // the plan takes one change at a time: a confirmation while one is under way removes
            // nothing and closes the dialog
            let handleConfirm =
                fun () ->
                    match confirmDeleteTarget, orderPlan with
                    | Some ncId, OrderPlanView.Settled(plan, _) ->
                        Api.OrderPlanCommand.RemoveOrderContexts(plan, [| ncId |]) |> planCommand
                    | None, _
                    | Some _, OrderPlanView.NoPatient
                    | Some _, OrderPlanView.Changing _ -> ()

                    setConfirmDeleteTarget None

            Components.ConfirmDialog.View
                {|
                    isOpen = isOpen
                    title =
                        Terms.``Nutrition Remove Enteral Title``
                        |> getTerm "Enterale voeding verwijderen"
                    text =
                        Terms.``Nutrition Remove Enteral Text``
                        |> getTerm
                            "Als u de enterale voeding verwijdert, worden ook alle bijbehorende supplementen verwijderd. Wilt u doorgaan?"
                    confirmLabel = Terms.Delete |> getTerm "Verwijderen"
                    cancelLabel = Terms.Cancel |> getTerm "Annuleren"
                    onConfirm = handleConfirm
                    onCancel = handleCancel
                |}

        let printDialog =
            match printOpen, orderPlan with
            | true, (OrderPlanView.Settled(plan, _) | OrderPlanView.Changing(plan, _)) ->
                ParenteralPrintView
                    {|
                        plan = plan
                        onClose = fun () -> setPrintOpen false
                    |}
            | false, _
            | true, OrderPlanView.NoPatient -> null

        JSX.jsx
            $"""
        import React from "react";
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Box>
            {patientNotice}
            {content}
            {confirmDeleteDialog}
            {printDialog}
        </Box>
        """
