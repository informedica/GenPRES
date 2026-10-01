namespace Views

#nowarn "1104"

/// The parenteral part of the nutrition page: TPN, lipids and electrolytes with glucose, each
/// a slot that folds, with its components, and the sheet to print them on.
module ParenteralNutrition =

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


    [<JSX.Component>]
    let private PrintView
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


    /// One parenteral order: in a fold with its values in the summary and a bin beside them,
    /// the composition with the dose type, the components, and the administration.
    [<JSX.Component>]
    let private SlotView
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
                isRecalculating: bool
            |})
        =
        let slot =
            useSlot
                {|
                    nutritionContext = props.nutritionContext
                    plan = props.plan
                    planCommand = props.planCommand
                    planReopen = props.planReopen
                    planRestore = props.planRestore
                    localizationTerms = props.localizationTerms
                    isRecalculating = props.isRecalculating
                    fieldMinWidth = Some 400
                |}

        let frequencyControl = slot.FrequencyControl
        let doseQtyControl = slot.DoseQuantityControl
        let genericFilter = slot.GenericFilter
        let doseTypeFilter = slot.DoseTypeFilter

        let frequencyDoseRow =
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
            JSX.jsx
                $"""
            import Divider from '@mui/material/Divider';
            <>
                {slot.ComponentHeader}
                {slot.ComponentRows |> unbox<seq<ReactElement>> |> React.Fragment}
                <Divider />
                {slot.TotalVolumeDisplay}
            </>
            """

        let details =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            <Stack direction={"column"} spacing={1} >
                {preparationSection}
                {slot.AdministrationHeading}
                {frequencyDoseRow}
                {slot.RateControl}
                {slot.ResetBar}
            </Stack>
            """
            |> fun fields ->
                // the spinner lies over the details and takes no room, so the fields stay where
                // they are while the order reloads
                Components.LoadingOverlay.View
                    {|
                        isLoading = slot.IsLoading
                        children = fields
                    |}

        let filterSx = {| marginBottom = 2 |}

        // the composition and the dose type side by side, once there is a dose type to choose
        let compositionRow =
            if isNull doseTypeFilter then
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
                {if isNull compositionRow then
                     genericFilter
                 else
                     compositionRow}
                {slot.IndicationFilter}
            </Stack>
            """

        let orderDetails = if slot.Order.IsSome then details else slot.LoadingIndicator

        let expanded, setExpanded = React.useState true

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

        let summary =
            let adminSummary =
                match props.nutritionContext.Scenarios with
                | [| sc |] when sc.Administration |> Array.isEmpty |> not ->
                    adminSummary sc.Order.Orderable.Name sc.Administration
                | _ -> null

            JSX.jsx
                $"""
            import Typography from '@mui/material/Typography';
            import Box from '@mui/material/Box';

            <Box sx={flexOverflowSx}>
                <Typography>{slot.Label}</Typography>
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
                isMobile = slot.IsMobile
                detailsPaddingTop = if slot.IsMobile then None else Some 4
                ariaControls = None
                summaryId = None
            |}


    /// The parenteral section: its heading with the button that prints the orders, a fold per
    /// order, and the buttons that add one.
    [<JSX.Component>]
    let Section
        (props:
            {|
                plan: OrderPlan
                planCommand: Api.OrderPlanCommand -> unit
                planReopen: Api.OrderPlanCommand -> unit
                planRestore: unit -> unit
                localizationTerms: Deferred<string[][]>
                isRecalculating: bool
            |})
        =
        let context: Global.Context = React.useContext Global.context
        let getTerm = Global.getLocalizedTerm props.localizationTerms context.Localization

        let printOpen, setPrintOpen = React.useState false

        let plan = props.plan
        let contexts = plan.OrderContexts |> Array.filter (isOneOf parenteral)

        let slotOf onRemove (nc: OrderContext) =
            SlotView
                {|
                    nutritionContext = nc
                    plan = props.plan
                    planCommand = props.planCommand
                    planReopen = props.planReopen
                    planRestore = props.planRestore
                    localizationTerms = props.localizationTerms
                    onRemove = Some onRemove
                    isRecalculating = props.isRecalculating
                |}

        let slots =
            contexts
            |> Array.map (fun nc ->
                slotOf (fun () -> Api.OrderPlanCommand.RemoveOrderContexts(plan, [| nc.Id |]) |> props.planCommand) nc
            )

        let newOrderContext category =
            Api.OrderPlanCommand.NewOrderContext(plan, category) |> props.planCommand

        // the buttons the plan admits a context for, as the server would rule
        let addButtons =
            [|
                NutritionCategory.TPN, Terms.``Nutrition TPN`` |> getTerm "TPN"
                NutritionCategory.Lipid, Terms.``Nutrition Lipids`` |> getTerm "Vetten"
                NutritionCategory.ElectrolyteGlucose,
                Terms.``Nutrition Electrolytes Glucose`` |> getTerm "Elektrolyten/Glucose"
            |]
            |> Array.filter (fun (category, _) -> plan |> OrderPlan.mayAdd category)
            |> Array.map (fun (category, label) ->
                Components.AddButton.View
                    {|
                        label = label
                        onClick = fun () -> newOrderContext category
                        disabled = props.isRecalculating
                    |}
            )

        let printDisabled = contexts |> Array.isEmpty

        let printDialog =
            if printOpen then
                PrintView
                    {|
                        plan = plan
                        onClose = fun () -> setPrintOpen false
                    |}
            else
                null

        JSX.jsx
            $"""
        import React from 'react';
        import Stack from '@mui/material/Stack';
        import Typography from '@mui/material/Typography';
        import Button from '@mui/material/Button';
        import PrintIcon from '@mui/icons-material/Print';

        <React.Fragment>
            <Stack direction="row" sx={alignCenterSx} spacing={1}>
                <Typography variant="h6">{Terms.``Nutrition Parenteral Section`` |> getTerm "Parenteraal"}</Typography>
                <Button color="primary" size="small" disabled={printDisabled} onClick={fun _ -> setPrintOpen true} startIcon={{<PrintIcon />}}>
                    {Terms.Print |> getTerm "Print"}
                </Button>
            </Stack>
            {slots |> unbox<seq<ReactElement>> |> React.Fragment}
            <Stack direction="row" spacing={1}>
                {addButtons |> unbox<seq<ReactElement>> |> React.Fragment}
            </Stack>
            {printDialog}
        </React.Fragment>
        """
