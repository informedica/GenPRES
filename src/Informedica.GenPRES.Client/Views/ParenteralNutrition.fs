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


    /// One parenteral order: in a fold with its values in the summary and a bin beside them,
    /// the composition with the dose type, the components, and the administration.
    [<JSX.Component>]
    let private SlotView
        (props:
            {|
                nutritionContext: OrderContext
                plan: OrderPlan
                evaluateContext: string * Api.OrderViewCommand -> unit
                // the list of a recalculation closed without a pick
                planRestore: unit -> unit
                localizationTerms: Deferred<string[][]>
                onRemove: (unit -> unit) option
                // the order into the plan; none while the slot cannot be prescribed
                onPrescribe: (unit -> unit) option
                busy: bool
            |})
        =
        let slot =
            useSlot
                {|
                    nutritionContext = props.nutritionContext
                    plan = props.plan
                    evaluateContext = props.evaluateContext
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
                Components.IntakeSlider.View
                    {|
                        labelId = $"intake-label-%s{props.nutritionContext.Id}"
                        share = slot.DoseQuantityShare
                        disabled = not slot.CanSetDoseShare || held
                        onCommit = slot.SetDoseQuantityPerc
                        order = box slot.Order
                    |}
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

        // every slot ends in one bar: reset on the left, prescribe on the right
        let actionBar = prescribeBar getTerm slot.ResetAction props.onPrescribe

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
                {slot.ScenarioNotice}
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


    /// The parenteral section: its heading with the button that prints the slots, a fold per slot, and
    /// the buttons that start one.
    [<JSX.Component>]
    let Section
        (props:
            {|
                plan: OrderPlan
                slots: Informedica.GenPRES.Client.Core.Client.SlotView list
                nutrition: AppEnv.INutrition
                // a change to the plan under way: no slot is prescribed meanwhile
                planChanging: bool
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

        let slotsShown =
            props.slots
            |> List.choose (fun slot -> contextOf slot |> Option.map (fun ctx -> slot, ctx))
            |> List.filter (snd >> isOneOf parenteral)

        // the print shows the parenteral orders of the plan, then the slots on the page, with the totals the
        // page shows, which count the slots narrowed to one order
        let printed =
            Array.append
                (plan.OrderContexts |> Array.filter (isOneOf parenteral))
                (slotsShown |> List.map snd |> List.toArray)

        // a slot whose prescribe is under way takes nothing, its bin included
        let slotOf (slot: Informedica.GenPRES.Client.Core.Client.SlotView, nc: OrderContext) =
            SlotView
                {|
                    nutritionContext = nc
                    plan = plan
                    evaluateContext = fun (_, cmd) -> props.nutrition.SlotCommand(slot.Id, cmd)
                    planRestore = fun () -> props.nutrition.SlotRestore slot.Id
                    localizationTerms = props.localizationTerms
                    onRemove = Some(fun () -> props.nutrition.RemoveSlot slot.Id)
                    onPrescribe = slot |> prescribeOf props.nutrition props.planChanging held
                    busy = props.busy || slot.Prescribing
                |}

        let slots = slotsShown |> List.map slotOf |> List.toArray

        let addButtons =
            [|
                NutritionCategory.TPN, Terms.``Nutrition TPN`` |> getTerm "TPN"
                NutritionCategory.Lipid, Terms.``Nutrition Lipids`` |> getTerm "Vetten"
                NutritionCategory.ElectrolyteGlucose,
                Terms.``Nutrition Electrolytes Glucose`` |> getTerm "Elektrolyten/Glucose"
            |]
            |> Array.map (fun (category, label) ->
                Components.AddButton.View
                    {|
                        label = label
                        onClick = fun () -> props.nutrition.StartSlot category
                        disabled = held
                    |}
            )

        let printDisabled = printed |> Array.isEmpty

        let printDialog =
            if printOpen then
                let totals = props.nutrition.Totals |> Option.defaultValue plan.Totals

                ParenteralPrint.View
                    {|
                        plan =
                            { plan with
                                OrderContexts = printed
                                Totals = totals
                            }
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
