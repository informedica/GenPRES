namespace Views


#nowarn "1104"

/// Shows the enteral part of the nutrition page: one collapsible section with the feeding slots,
/// each with its fields on one row and its supplements.
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


    /// Lays out the fields of a feeding or a supplement on one row: the first item, the frequency, the
    /// dose quantity and the dose per time, so a feeding and its supplements share the same columns.
    /// When the row is narrower than the items need, all items stack in one column; they never wrap
    /// part of the way and never scroll sideways. A container query on the row width decides this, not
    /// the window width. A field that has no buttons here drops the room for them.
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

        // The least row width: the first item with the room for a severity mark, the frequency with its
        // inner buttons, the dose with all its buttons, the dose per time without buttons, and the gaps.
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

        // The first item reserves the room for a severity mark, as the fields do, so all gaps look the
        // same.
        let itemSx =
            {|
                flex = "1 1 0%"
                minWidth = 200
                paddingRight = $"%i{Components.QuantityField.markWidth}px"
                ``& .MuiFormControl-root`` = {| width = "100%" |}
                ``& .MuiAutocomplete-root`` = {| minWidth = "unset" |}
            |}

        // Each column has the least width of the widest form of its field, whatever buttons the field
        // shows, so a feeding and its supplements get the same columns: the frequency with its inner
        // buttons, whether it is selected or stepped; the dose with all its buttons; the dose per time
        // without buttons.
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


    /// Shows one supplement offer that is switched on: its name with a remove button that switches it
    /// off, then the frequency, the dose and the dose per time in the columns of the feeding. It has no
    /// filters, because the link rule sets the composition, indication and dose type, and a nutrition
    /// order has no form to select.
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


    /// Shows the button that adds a supplement. It opens a menu with the names of the offers that are
    /// switched off; a selection switches that offer on. The menu is anchored to the box around the button.
    [<JSX.Component>]
    let private AddOfferMenu
        (props:
            {|
                label: string
                // The offers that are switched off, each with its index among all offers of the slot.
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


    /// Shows one feeding: its name with a remove button, the filters, the frequency, the dose and the dose
    /// per time on one row, the supplements its link rules offer, and the bar with the prescribe button.
    [<JSX.Component>]
    let private SlotView
        (props:
            {|
                nutritionContext: OrderContext
                plan: OrderPlan
                evaluateContext: string * Api.OrderViewCommand -> unit
                // Called when the recalculation list closes without a selection.
                planRestore: unit -> unit
                localizationTerms: Deferred<string[][]>
                onRemove: (unit -> unit) option
                // Adds the order to the plan; None while the slot cannot be prescribed.
                onPrescribe: (unit -> unit) option
                // The offers of the feeding that are switched on.
                offers: JSX.Element[]
                // The button with the menu of the offers that are switched off; None when every offer is on.
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

        // Once there is an order, the composition filter moves into the row with the fields.
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

        // The controls that send a request also wait while a field counts its step clicks. The page
        // itself stays enabled then, because the counting field is on the page.
        let counting = React.useContext Global.counting
        let waiting = props.busy || counting.Counting

        let removeButton =
            match props.onRemove with
            | Some onRemove ->
                // The remove button waits during a request, because the plan takes one change at a time.
                Components.RemoveIconButton.View
                    {|
                        label = Terms.``Delete`` |> getTerm "Delete"
                        onRemove = onRemove
                        busy = waiting
                    |}
            | None -> null

        // The heading shows the chosen feeding, or the category before one is chosen, with the remove
        // button beside it.
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


    /// Shows the enteral section: one collapsible panel with the feeding slots, the administration of
    /// each in its summary, and the button that starts a feeding slot. A supplement is offered inside a
    /// feeding slot and is not started on its own.
    [<JSX.Component>]
    let Section
        (props:
            {|
                plan: OrderPlan
                slots: Informedica.GenPRES.Client.Core.Client.SlotView list
                nutrition: AppEnv.INutrition
                // True while a change to the plan is under way; no slot can be prescribed then.
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

        // The controls that send a request also wait while a field counts its step clicks. The page
        // itself stays enabled then, because the counting field is on the page.
        let counting = React.useContext Global.counting
        let waiting = props.busy || counting.Counting

        let slotsShown =
            props.slots
            |> List.choose (fun slot -> contextOf slot |> Option.map (fun ctx -> slot, ctx))
            |> List.filter (snd >> isOneOf enteral)

        // A slot that is being prescribed takes no input, its remove button and its offers included.
        let slotOf (slot: Informedica.GenPRES.Client.Core.Client.SlotView, nc: OrderContext) =
            let busy = props.busy || slot.Prescribing

            // Each offer with its index among the offers of the slot; the commands name an offer by
            // this index.
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
                <Typography sx={summaryLabelSx}>{Terms.``Nutrition Enteral Feeding`` |> getTerm "Enteral feeding"}</Typography>
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

        let disclosure =
            Components.Disclosure.View
                {|
                    isOpen = expanded
                    onToggle = fun () -> setExpanded (not expanded)
                    summary = summary
                    children = children
                    summaryPlacement = Components.Disclosure.SummaryPlacement.Stacks
                    isMobile = isMobile
                    detailsPaddingTop = if isMobile then None else Some 4
                    ariaControls = None
                    summaryId = None
                |}

        JSX.jsx
            $"""
        import React from 'react';
        import Typography from '@mui/material/Typography';

        <React.Fragment>
            <Typography variant="h6">{Terms.``Nutrition Enteral Section`` |> getTerm "Enteral"}</Typography>
            {disclosure}
        </React.Fragment>
        """
