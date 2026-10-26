namespace Views


#nowarn "1104"

/// The parenteral part of the nutrition page: TPN, lipids and electrolytes with glucose, each
/// a slot that folds, with its components, and a button that prints them.
module ParenteralNutrition =

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
    open NutritionSlot


    let private autoMarginSx = {| marginLeft = "auto" |}


    // the steps of the intake slider, in percent of the full intake; only a few carry a label, so
    // the slider stays readable on a narrow screen
    let private intakeMarks =
        [|
            for v in 10..10..100 ->
                if v = 10 || v = 50 || v = 100 then
                    createObj [ "value" ==> v; "label" ==> $"%i{v}%%" ]
                else
                    createObj [ "value" ==> v ]
        |]


    let private toolbarSx =
        {|
            display = "flex"
            flexWrap = "wrap"
            width = "100%"
            justifyContent = "space-between"
            alignItems = "center"
            gap = 2
        |}


    // on the right the room of the severity mark of a field, so the slider is as wide as the
    // fields of its column; no room above it, so the total row is no higher than the others and
    // the value that pops up while it moves lies over the caption
    let private sliderBoxSx = {| paddingRight = $"%i{Components.QuantityField.markWidth}px" |}


    // a dose that is no step of the slider has no handle on it
    let private noHandleSx = {| ``& .MuiSlider-thumb`` = {| display = "none" |} |}


    /// One parenteral order: in a fold with its values in the summary and a bin beside them,
    /// the composition with the dose type, the components, and the administration.
    [<JSX.Component>]
    let private SlotView
        (props:
            {|
                nutritionContext: OrderContext
                planNavigate: string * Api.OrderViewCommand -> unit
                // a clear from a field's arrow, and the list of such a reopen closed without a pick
                planReopen: string * Api.OrderViewCommand -> unit
                planRestore: unit -> unit
                localizationTerms: Deferred<string[][]>
                onRemove: (unit -> unit) option
                busy: bool
            |})
        =
        let slot =
            useSlot
                {|
                    nutritionContext = props.nutritionContext
                    planNavigate = props.planNavigate
                    planReopen = props.planReopen
                    planRestore = props.planRestore
                    localizationTerms = props.localizationTerms
                    busy = props.busy
                    fieldMinWidth = None
                |}

        let context: Global.Context = React.useContext Global.context
        let getTerm = Global.getLocalizedTerm props.localizationTerms context.Localization

        // the page is not disabled while a field counts its step clicks, since the field counting
        // is on it; the controls that send wait for the count themselves
        let counting = React.useContext Global.counting
        let held = props.busy || counting.Counting

        // the position while the user drags; otherwise the slider reads it from the order, and a
        // new order drops it
        let dragged, setDragged = React.useState (None: int option)

        React.useEffect ((fun () -> setDragged None), [| box slot.Order |])

        let genericFilter = slot.GenericFilter
        let doseTypeFilter = slot.DoseTypeFilter

        // the dose, the rate and the time in the columns of the components, stacked as they are.
        // The time takes no buttons, so it drops their room.
        let administrationRow =
            let containerSx = {| Components.QuantityField.dropUnusedSlotsSx with alignItems = "flex-end" |}

            JSX.jsx
                $"""
            import Grid from '@mui/material/Grid';
            <Grid container spacing={{2}} sx={containerSx}>
                <Grid size={cmpLabelSize}>
                    {slot.DoseQuantityControl}
                </Grid>
                <Grid size={cmpQtySize} sx={cmpMiddleSx}>
                    {slot.RateField}
                </Grid>
                <Grid size={cmpRangeSize} sx={cmpRightSx}>
                    {slot.TimeDisplay}
                </Grid>
            </Grid>
            """

        // the share of the full intake the TPN gives, beside its total volume; it sets the dose once
        // the TPN is composed and its dose count holds one value
        let intakeSlider =
            if props.nutritionContext |> isOneOf [ NutritionCategory.TPN ] then
                let position =
                    dragged
                    |> Option.orElse (slot.DoseQuantityShare |> Option.bind IntakePolicy.sliderStep)
                let value = position |> Option.defaultValue 100
                let sliderSx = if position.IsSome then null else box noHandleSx
                let track: obj = if position.IsSome then box "normal" else box false
                let disabled = not slot.CanSetDoseQuantityPerc || held

                // a dose set otherwise shows its exact share instead of a handle
                let shareText =
                    match position, slot.DoseQuantityShare with
                    | None, Some share ->
                        let pct = share |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 3
                        $": %s{pct}%% van totaal"
                    | _ -> ""

                let onIntakeChange = System.Func<obj, int, unit>(fun _ v -> setDragged (Some v))
                let onIntakeCommitted = System.Func<obj, int, unit>(fun _ v -> slot.SetDoseQuantityPerc v)
                let intakeLabel = fun (v: int) -> $"%i{v}%% van totaal"

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import Slider from '@mui/material/Slider';
                import Typography from '@mui/material/Typography';
                <Box sx={sliderBoxSx}>
                    <Typography id="tpn-intake-label" variant="caption" color="text.secondary">
                        toedien hoeveelheid als percentage van totaal{shareText}
                    </Typography>
                    <Slider
                        aria-labelledby="tpn-intake-label"
                        value={value}
                        onChange={onIntakeChange}
                        onChangeCommitted={onIntakeCommitted}
                        step={10}
                        min={10}
                        max={100}
                        marks={intakeMarks}
                        track={track}
                        sx={sliderSx}
                        valueLabelDisplay="auto"
                        valueLabelFormat={intakeLabel}
                        disabled={disabled}
                    />
                </Box>
                """
            else
                null

        // the total volume stands in the columns of the components, under their quantities, with
        // the intake in the column of their ranges
        let totalVolumeRow =
            JSX.jsx
                $"""
            import Grid from '@mui/material/Grid';
            import Box from '@mui/material/Box';
            import Typography from '@mui/material/Typography';
            <Grid container spacing={{2}} sx={alignCenterSx}>
                <Grid size={cmpLabelSize}>
                    <Typography variant="body1">Totaal volume</Typography>
                </Grid>
                <Grid size={cmpQtySize} sx={cmpMiddleSx}>
                    <Box sx={cmpQtySx}>
                        {slot.TotalVolumeDisplay}
                    </Box>
                </Grid>
                <Grid size={cmpRangeSize}>
                    {intakeSlider}
                </Grid>
            </Grid>
            """

        // the TPN ends in one bar: reset on the left, prescribe on the right; prescribe does nothing
        // yet
        let tpnToolbar =
            let resetButton = Components.ActionBar.ActionButton slot.ResetAction

            let prescribeButton =
                Components.ActionBar.ActionButton
                    {|
                        label = Terms.``Prescribe`` |> getTerm "Voorschrijven"
                        kind = Components.ActionBar.Kind.Primary
                        onClick = ignore
                        disabled = false
                        icon = None
                    |}

            JSX.jsx
                $"""
            import Box from '@mui/material/Box';
            <Box sx={toolbarSx}>
                {resetButton}
                {prescribeButton}
            </Box>
            """

        let actionBar =
            if props.nutritionContext |> isOneOf [ NutritionCategory.TPN ] then
                tpnToolbar
            else
                slot.ResetBar

        // a little more room above the divider under the components; padding, because the Stack
        // it sits in sets its top margin, and the line is drawn at its bottom
        let dividerSx = {| paddingTop = 1 |}

        let preparationSection =
            JSX.jsx
                $"""
            import Divider from '@mui/material/Divider';
            <>
                {slot.ComponentHeader}
                {slot.ComponentRows |> unbox<seq<ReactElement>> |> React.Fragment}
                {slot.CompositionHint}
                <Divider sx={dividerSx} />
                {totalVolumeRow}
            </>
            """

        // padding, because the Stack the bar sits in sets the top margin of its children
        let actionBarSx = {| paddingTop = 1 |}

        let details =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            import Box from '@mui/material/Box';
            <Stack direction={"column"} spacing={1} >
                {preparationSection}
                {slot.AdministrationHeading}
                {administrationRow}
                <Box sx={actionBarSx}>
                    {actionBar}
                </Box>
            </Stack>
            """

        let filterSx = {| marginBottom = 2 |}

        // on a large screen the composition spans the component and quantity columns and ends
        // where the quantity fields end, before the room of their severity mark
        let compositionSize =
            {|
                xs = 12
                md = 6
                lg = 7.5
            |}

        let doseTypeSize =
            {|
                xs = 12
                md = 6
                lg = 4.5
            |}

        let compositionSx =
            {|
                paddingRight =
                    {|
                        xs = "0px"
                        lg = $"%i{Components.QuantityField.markWidth}px"
                    |}
            |}

        // the composition and the dose type side by side, once there is a dose type to choose
        let compositionRow =
            if isNull doseTypeFilter then
                null
            else
                JSX.jsx
                    $"""
                import Grid from '@mui/material/Grid';
                <Grid container spacing={{2}}>
                    <Grid size={compositionSize} sx={compositionSx}>
                        {genericFilter}
                    </Grid>
                    <Grid size={doseTypeSize}>
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

        let orderDetails = if slot.Order.IsSome then details else null

        let expanded, setExpanded = React.useState true

        let removeButton =
            match props.onRemove with
            | Some onRemove ->
                // the plan takes one change at a time: no removal while one is under way
                Components.RemoveIconButton.View
                    {|
                        onRemove = onRemove
                        busy = held
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
                planNew: NutritionCategory -> unit
                planRemove: string[] -> unit
                planReopen: string * Api.OrderViewCommand -> unit
                planNavigate: string * Api.OrderViewCommand -> unit
                planRestore: unit -> unit
                localizationTerms: Deferred<string[][]>
                busy: bool
            |})
        =
        let context: Global.Context = React.useContext Global.context
        let getTerm = Global.getLocalizedTerm props.localizationTerms context.Localization

        // the page is not disabled while a field counts its step clicks, since the field counting
        // is on it; the controls that send wait for the count themselves
        let counting = React.useContext Global.counting
        let held = props.busy || counting.Counting

        let printOpen, setPrintOpen = React.useState false

        let plan = props.plan
        let contexts = plan.OrderContexts |> Array.filter (isOneOf parenteral)

        let slotOf onRemove (nc: OrderContext) =
            SlotView
                {|
                    nutritionContext = nc
                    planNavigate = props.planNavigate
                    planReopen = props.planReopen
                    planRestore = props.planRestore
                    localizationTerms = props.localizationTerms
                    onRemove = Some onRemove
                    busy = props.busy
                |}

        let slots =
            contexts
            |> Array.map (fun nc -> slotOf (fun () -> props.planRemove [| nc.Id |]) nc)

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
                        onClick = fun () -> props.planNew category
                        disabled = held
                    |}
            )

        let printDisabled = contexts |> Array.isEmpty

        let printDialog =
            if printOpen then
                ParenteralPrint.View
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
