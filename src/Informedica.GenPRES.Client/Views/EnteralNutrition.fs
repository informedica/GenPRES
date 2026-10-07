namespace Views

#nowarn "1104"

/// The enteral part of the nutrition page: the feedings and the supplements, each a slot with
/// its fields on one row, in one section that folds.
module EnteralNutrition =

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


    /// One feeding or supplement: its name with a bin beside it, the filters, and the
    /// frequency, the dose and the dose per time on one row.
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
                isRecalculating: bool
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
                    isRecalculating = props.isRecalculating
                    fieldMinWidth = None
                |}

        let genericFilter = slot.GenericFilter
        let frequencyControl = slot.FrequencyControl
        let doseQtyControl = slot.DoseQuantityControl
        let dosePerTimeAdjDisplay = slot.DosePerTimeAdjustDisplay

        let frequencyDoseRow =
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

        let details =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            <Stack direction={"column"} spacing={1} >
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

        // the composition stands in the row with the fields once there is an order
        let filterControls =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            <Stack direction="column" spacing={2} sx={filterSx}>
                {if slot.Order.IsSome then null else genericFilter}
                {slot.IndicationFilter}
                {slot.DoseTypeFilter}
            </Stack>
            """

        let orderDetails = if slot.Order.IsSome then details else slot.LoadingIndicator

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

        // the section's name on its line, with the button that removes the section beside it
        let heading =
            Components.SectionHeading.View
                {|
                    label = slot.Label
                    action = Some removeButton
                |}

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';

        <Box>
            {heading}
            {filterControls}
            {orderDetails}
        </Box>
        """


    /// The enteral section: the feedings and supplements in one fold, the values of each in its
    /// summary, the buttons that add one, and the question before a feeding with supplements is
    /// removed.
    [<JSX.Component>]
    let Section
        (props:
            {|
                plan: OrderPlan
                planCommand: Api.OrderPlanCommand -> unit
                planReopen: string * Api.OrderViewCommand -> unit
                planNavigate: string * Api.OrderViewCommand -> unit
                planRestore: unit -> unit
                localizationTerms: Deferred<string[][]>
                isRecalculating: bool
            |})
        =
        let context: Global.Context = React.useContext Global.context
        let getTerm = Global.getLocalizedTerm props.localizationTerms context.Localization

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let expanded, setExpanded = React.useState true
        let confirmDeleteTarget, setConfirmDeleteTarget = React.useState<string option> None

        let plan = props.plan
        let contexts = plan.OrderContexts |> Array.filter (isOneOf enteral)

        // removing a feeding removes its supplements too, so it asks first when there are any
        let removeOf (nc: OrderContext) =
            let hasSupplements =
                nc |> isOneOf [ NutritionCategory.EnteralFeeding ]
                && plan.OrderContexts
                   |> Array.exists (isOneOf [ NutritionCategory.EnteralSupplement ])

            fun () ->
                if hasSupplements then
                    setConfirmDeleteTarget (Some nc.Id)
                else
                    Api.OrderPlanCommand.RemoveOrderContexts(plan, [| nc.Id |]) |> props.planCommand

        let slotOf (nc: OrderContext) =
            SlotView
                {|
                    nutritionContext = nc
                    planNavigate = props.planNavigate
                    planReopen = props.planReopen
                    planRestore = props.planRestore
                    localizationTerms = props.localizationTerms
                    onRemove = Some(removeOf nc)
                    isRecalculating = props.isRecalculating
                |}

        let slots = contexts |> Array.map slotOf

        let newOrderContext category =
            Api.OrderPlanCommand.NewOrderContext(plan, category) |> props.planCommand

        // the buttons the plan admits a context for, as the server would rule
        let addButton category label =
            if plan |> OrderPlan.mayAdd category then
                Components.AddButton.View
                    {|
                        label = label
                        onClick = fun () -> newOrderContext category
                        disabled = props.isRecalculating
                    |}
            else
                null

        let feedingAddButton =
            Terms.``Nutrition Enteral Feeding``
            |> getTerm "Enterale Voeding"
            |> addButton NutritionCategory.EnteralFeeding

        let supplementAddButton =
            Terms.``Nutrition Add Supplement``
            |> getTerm "Supplement toevoegen"
            |> addButton NutritionCategory.EnteralSupplement

        let summary =
            let adminSummaries =
                contexts
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
                {slots |> unbox<seq<ReactElement>> |> React.Fragment}
                {feedingAddButton}
                {supplementAddButton}
            </Stack>
            """

        let disclosure =
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

        let confirmDeleteDialog =
            // the plan takes one change at a time: a confirmation while one is under way removes
            // nothing and closes the dialog
            let handleConfirm =
                fun () ->
                    match confirmDeleteTarget with
                    | Some ncId when not props.isRecalculating ->
                        Api.OrderPlanCommand.RemoveOrderContexts(plan, [| ncId |]) |> props.planCommand
                    | Some _
                    | None -> ()

                    setConfirmDeleteTarget None

            Components.ConfirmDialog.View
                {|
                    isOpen = confirmDeleteTarget.IsSome
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
                    onCancel = fun () -> setConfirmDeleteTarget None
                |}

        JSX.jsx
            $"""
        import React from 'react';

        <React.Fragment>
            {disclosure}
            {confirmDeleteDialog}
        </React.Fragment>
        """
