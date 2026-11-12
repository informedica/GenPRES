namespace Views


#nowarn "1104"

/// The enteral part of the nutrition page: the feedings and the supplements, each a slot with
/// its fields on one row, in one section that folds.
module EnteralNutrition =

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


    /// The fields of a feeding or a supplement on one measured row: the first item, then the frequency,
    /// the dose quantity and the dose per time, so that a feeding and its supplements share the columns.
    /// Narrower than the items need, all items stand in one column, never wrapped part-way and never
    /// scrolled sideways. The row is measured, not the window: the container query decides, and a field
    /// that never gets buttons here drops its hidden slots.
    let private fieldRow
        (first: JSX.Element)
        (frequency: JSX.Element)
        (doseQuantity: JSX.Element)
        (dosePerTime: JSX.Element)
        =
        let containerSx =
            {| Components.QuantityField.dropUnusedSlotsSx with
                containerType = "inline-size"
                width = "100%"
            |}

        // the first item with the room of a severity mark, frequency with its inner buttons, the dose
        // field with all its buttons, dose per time without, and the gaps
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

        // the first item keeps the room a field gives its severity mark, so the gaps between all items
        // look the same
        let itemSx =
            {|
                flex = "1 1 0%"
                minWidth = 200
                paddingRight = $"%i{Components.QuantityField.markWidth}px"
                ``& .MuiFormControl-root`` = {| width = "100%" |}
                ``& .MuiAutocomplete-root`` = {| minWidth = "unset" |}
            |}

        // every column keeps the least width of its widest form, whatever buttons its field shows, so that
        // the rows of a feeding and its supplements get the same columns: frequency with its inner
        // buttons, whether it is chosen from the dropdown or stepped; the dose with all its buttons; the
        // dose per time without
        let columnSx minWidth =
            {|
                flex = "1 1 0%"
                minWidth = minWidth
            |}

        let frequencyItemSx = columnSx Components.QuantityField.fieldWidthInnerSlots
        let doseQuantityItemSx = columnSx Components.QuantityField.fieldWidth
        let dosePerTimeItemSx = columnSx Components.QuantityField.fieldWidthWithoutSlots

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        <Box sx={containerSx}>
            <Box sx={flexSx}>
                <Box sx={itemSx}>
                    {first}
                </Box>
                <Box sx={frequencyItemSx}>
                    {frequency}
                </Box>
                <Box sx={doseQuantityItemSx}>
                    {doseQuantity}
                </Box>
                <Box sx={dosePerTimeItemSx}>
                    {dosePerTime}
                </Box>
            </Box>
        </Box>
        """


    /// One offer of a feeding that is on: its name with the bin that turns it off, the frequency, the
    /// dose and the dose per time in the columns of the feeding's fields. It shows no filter: the link
    /// rule decides its composition, its indication and its dose type, and a nutrition slot has no form
    /// to pick.
    [<JSX.Component>]
    let private OfferView
        (props:
            {|
                offer: OrderContextMachine.LinkedOffer
                index: int
                slotId: string
                nutrition: AppEnv.INutrition
                plan: OrderPlan
                localizationTerms: Deferred<string[][]>
                busy: bool
            |})
        =
        let slot =
            useSlot
                {|
                    nutritionContext = props.offer.Context
                    plan = props.plan
                    evaluateContext = fun (_, cmd) -> props.nutrition.OfferCommand(props.slotId, props.index, cmd)
                    planRestore = fun () -> props.nutrition.SlotRestore props.slotId
                    localizationTerms = props.localizationTerms
                    busy = props.busy
                    fieldMinWidth = None
                |}

        let context: Global.Context = React.useContext Global.context
        let getTerm = Global.getLocalizedTerm props.localizationTerms context.Localization

        let counting = React.useContext Global.counting
        let waiting = props.busy || counting.Counting

        let bin =
            Components.RemoveIconButton.View
                {|
                    label = Terms.``Delete`` |> getTerm "Delete"
                    onRemove =
                        fun () ->
                            props.nutrition.TurnOffer(props.slotId, props.index, OrderContextMachine.OfferSwitch.Off)
                    busy = waiting
                |}

        let nameSx =
            {|
                display = "flex"
                alignItems = "center"
                minHeight = 40
            |}

        let nameWithBin =
            JSX.jsx
                $"""
            import Box from '@mui/material/Box';
            import Typography from '@mui/material/Typography';

            <Box sx={nameSx}>
                <Typography>{OrderContextText.offerName props.offer.Context}</Typography>
                {bin}
            </Box>
            """

        JSX.jsx
            $"""
        import Stack from '@mui/material/Stack';
        <Stack direction="column" spacing={1}>
            {slot.ScenarioNotice}
            {fieldRow nameWithBin slot.FrequencyControl slot.DoseQuantityControl slot.DosePerTimeAdjustDisplay}
        </Stack>
        """


    /// The button that adds a supplement: it opens a menu of the offers that are off, by name, and a pick
    /// turns that offer on. The menu hangs from the box around the button.
    [<JSX.Component>]
    let private AddOfferMenu
        (props:
            {|
                label: string
                // the offers that are off, each with its index among the offers of the slot
                offers: (int * string)[]
                onPick: int -> unit
                disabled: bool
            |})
        =
        let anchor = React.useElementRef ()
        let isOpen, setOpen = React.useState false

        let handleClose _ = setOpen false

        let pick index _ =
            setOpen false
            props.onPick index

        let items =
            props.offers
            |> Array.map (fun (index, name) ->
                JSX.jsx
                    $"""
                import MenuItem from '@mui/material/MenuItem';
                <MenuItem key={index} onClick={pick index}>{name}</MenuItem>
                """
            )

        let addButton =
            Components.AddButton.View
                {|
                    label = props.label
                    onClick = fun () -> setOpen true
                    disabled = props.disabled
                |}

        JSX.jsx
            $"""
        import React from 'react';
        import Box from '@mui/material/Box';
        import Menu from '@mui/material/Menu';

        <React.Fragment>
            <Box component="span" ref={anchor}>
                {addButton}
            </Box>
            <Menu anchorEl={anchor.current} open={isOpen} onClose={handleClose}>
                {items |> unbox<seq<ReactElement>> |> React.Fragment}
            </Menu>
        </React.Fragment>
        """


    /// One feeding: its name with a bin beside it, the filters, the frequency, the dose and the dose per
    /// time on one row, the supplements its link rules offer, and the bar with the prescribe.
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
                // the offers of the feeding that are on
                offers: JSX.Element[]
                // the button with the menu of the offers that are off; none while every offer is on
                addOffer: JSX.Element option
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

        let genericFilter = slot.GenericFilter
        let frequencyControl = slot.FrequencyControl
        let doseQtyControl = slot.DoseQuantityControl
        let dosePerTimeAdjDisplay = slot.DosePerTimeAdjustDisplay

        let frequencyDoseRow = fieldRow genericFilter frequencyControl doseQtyControl dosePerTimeAdjDisplay

        let context: Global.Context = React.useContext Global.context
        let getTerm = Global.getLocalizedTerm props.localizationTerms context.Localization

        let offersHeading =
            if props.offers |> Array.isEmpty && props.addOffer.IsNone then
                null
            else
                Components.SectionHeading.View
                    {|
                        label = Terms.``Nutrition Enteral Supplements`` |> getTerm "Enteral supplements"
                        action = None
                    |}

        let details =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            <Stack direction={"column"} spacing={1} >
                {frequencyDoseRow}
                {slot.RateControl}
                {offersHeading}
                {props.offers |> unbox<seq<ReactElement>> |> React.Fragment}
                {props.addOffer |> Option.defaultValue null}
                {prescribeBar getTerm slot.ResetAction props.onPrescribe}
            </Stack>
            """

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
                {slot.ScenarioNotice}
            </Stack>
            """

        let orderDetails = if slot.Order.IsSome then details else null

        // the page is not disabled while a field counts its step clicks, since the field counting
        // is on it; the controls that send wait for the count themselves
        let counting = React.useContext Global.counting
        let waiting = props.busy || counting.Counting

        let removeButton =
            match props.onRemove with
            | Some onRemove ->
                // the plan takes one change at a time: no removal while one is under way
                Components.RemoveIconButton.View
                    {|
                        label = Terms.``Delete`` |> getTerm "Delete"
                        onRemove = onRemove
                        busy = waiting
                    |}
            | None -> null

        // the name of the chosen feeding on its line, the category before one is chosen, with the
        // button that removes the section beside it
        let heading =
            Components.SectionHeading.View
                {|
                    label = props.nutritionContext.Filter.Generic |> Option.defaultValue slot.Label
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


    /// The enteral section: the feeding slots in one fold, the values of each in its summary, and the
    /// button that starts one. A supplement is offered in a feeding slot, not started on its own.
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

        let isMobile = Mui.Hooks.useIsMobile ()

        let expanded, setExpanded = React.useState true

        let plan = props.plan

        // the page is not disabled while a field counts its step clicks, since the field counting
        // is on it; the controls that send wait for the count themselves
        let counting = React.useContext Global.counting
        let waiting = props.busy || counting.Counting

        let slotsShown =
            props.slots
            |> List.choose (fun slot -> contextOf slot |> Option.map (fun ctx -> slot, ctx))
            |> List.filter (snd >> isOneOf enteral)

        // a slot whose prescribe is under way takes nothing, its bin and its offers included
        let slotOf (slot: Informedica.GenPRES.Client.Core.Client.SlotView, nc: OrderContext) =
            let busy = props.busy || slot.Prescribing

            // each offer with its place among the offers of the slot, which keys its state
            let offersOn, offersOff =
                slot.Offers
                |> Array.indexed
                |> Array.partition (fun (_, offer) -> offer.Switch = OrderContextMachine.OfferSwitch.On)

            let offers =
                offersOn
                |> Array.map (fun (index, offer) ->
                    let view =
                        OfferView
                            {|
                                offer = offer
                                index = index
                                slotId = slot.Id
                                nutrition = props.nutrition
                                plan = plan
                                localizationTerms = props.localizationTerms
                                busy = busy
                            |}

                    JSX.jsx
                        $"""
                    import React from 'react';
                    <React.Fragment key={index}>{view}</React.Fragment>
                    """
                )

            let addOffer =
                if offersOff |> Array.isEmpty then
                    None
                else
                    AddOfferMenu
                        {|
                            label = Terms.``Nutrition Add Supplement`` |> getTerm "Add supplement"
                            offers =
                                offersOff
                                |> Array.map (fun (index, offer) -> index, OrderContextText.offerName offer.Context)
                            onPick =
                                fun index ->
                                    props.nutrition.TurnOffer(slot.Id, index, OrderContextMachine.OfferSwitch.On)
                            disabled = busy || waiting
                        |}
                    |> Some

            SlotView
                {|
                    nutritionContext = nc
                    plan = plan
                    evaluateContext = fun (_, cmd) -> props.nutrition.SlotCommand(slot.Id, cmd)
                    planRestore = fun () -> props.nutrition.SlotRestore slot.Id
                    localizationTerms = props.localizationTerms
                    onRemove = Some(fun () -> props.nutrition.RemoveSlot slot.Id)
                    onPrescribe = slot |> prescribeOf props.nutrition props.planChanging waiting
                    offers = offers
                    addOffer = addOffer
                    busy = busy
                |}

        let slots = slotsShown |> List.map slotOf |> List.toArray

        let feedingAddButton =
            Components.AddButton.View
                {|
                    label = Terms.``Nutrition Enteral Feeding`` |> getTerm "Enteral feeding"
                    onClick = fun () -> props.nutrition.StartSlot NutritionCategory.EnteralFeeding
                    disabled = waiting
                |}

        let summary =
            let adminSummaries =
                slotsShown
                |> List.choose (fun (slot, nc) ->
                    match nc.Scenarios with
                    | [| sc |] when sc.Administration |> Array.isEmpty |> not ->
                        (slot.Id, adminSummary sc.Order.Orderable.Name sc.Administration) |> Some
                    | _ -> None
                )
                |> List.toArray
                |> withKey

            JSX.jsx
                $"""
            import Typography from '@mui/material/Typography';
            import Box from '@mui/material/Box';

            <Box sx={flexOverflowSx}>
                <Typography>{Terms.``Nutrition Enteral Feeding`` |> getTerm "Enteral feeding"}</Typography>
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
            </Stack>
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
