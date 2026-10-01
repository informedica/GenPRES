namespace Views

#nowarn "1104"

/// The parenteral part of the nutrition page: TPN, lipids and electrolytes with glucose, each
/// a slot that folds, with its components, and a button that prints them.
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


    let private autoMarginSx = {| marginLeft = "auto" |}


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
