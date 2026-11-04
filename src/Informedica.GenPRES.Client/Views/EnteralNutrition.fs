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


    // the room of the switch before an offer's controls, so they stand under its name
    let private offerControlsSx = {| paddingLeft = 6 |}


    /// One offer of a feeding: its name with the switch that turns it on, and while it is on the frequency,
    /// the dose and the dose per time. It shows no filter: the link rule decides its composition, its
    /// indication and its dose type, and a nutrition slot has no form to pick.
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

        let counting = React.useContext Global.counting
        let held = props.busy || counting.Counting

        let isOn = props.offer.Switch = OrderContextMachine.OfferSwitch.On

        let name =
            props.offer.Context.Filter.Generic
            |> Option.defaultValue (OrderContextText.label props.offer.Context)

        let switchTo =
            if isOn then
                OrderContextMachine.OfferSwitch.Off
            else
                OrderContextMachine.OfferSwitch.On

        let handleSwitch _ = props.nutrition.TurnOffer(props.slotId, props.index, switchTo)

        let controls =
            if isOn then
                JSX.jsx
                    $"""
                import Stack from '@mui/material/Stack';
                import Box from '@mui/material/Box';
                <Box sx={offerControlsSx}>
                    <Stack direction="column" spacing={2}>
                        {slot.ScenarioNotice}
                        <Stack direction="row" spacing={2}>
                            {slot.FrequencyControl}
                            {slot.DoseQuantityControl}
                            {slot.DosePerTimeAdjustDisplay}
                        </Stack>
                    </Stack>
                </Box>
                """
            else
                null

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import FormControlLabel from '@mui/material/FormControlLabel';
        import Switch from '@mui/material/Switch';

        <Box>
            <FormControlLabel
                control={{<Switch checked={isOn} disabled={held} onChange={handleSwitch} />}}
                label={name}
            />
            {controls}
        </Box>
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
                // the offers of the feeding, each with its switch
                offers: JSX.Element[]
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

        let context: Global.Context = React.useContext Global.context
        let getTerm = Global.getLocalizedTerm props.localizationTerms context.Localization

        let offersHeading =
            if props.offers |> Array.isEmpty then
                null
            else
                Components.SectionHeading.View
                    {|
                        label = Terms.``Nutrition Enteral Supplement`` |> getTerm "Supplementen"
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
        let held = props.busy || counting.Counting

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
        let held = props.busy || counting.Counting

        let slotsShown =
            props.slots
            |> List.choose (fun slot -> contextOf slot |> Option.map (fun ctx -> slot, ctx))
            |> List.filter (snd >> isOneOf enteral)

        // a slot whose prescribe is under way takes nothing, its bin and its offers included
        let slotOf (slot: Informedica.GenPRES.Client.Core.Client.SlotView, nc: OrderContext) =
            let busy = props.busy || slot.Prescribing

            let offers =
                slot.Offers
                |> Array.mapi (fun index offer ->
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
                )

            SlotView
                {|
                    nutritionContext = nc
                    plan = plan
                    evaluateContext = fun (_, cmd) -> props.nutrition.SlotCommand(slot.Id, cmd)
                    planRestore = fun () -> props.nutrition.SlotRestore slot.Id
                    localizationTerms = props.localizationTerms
                    onRemove = Some(fun () -> props.nutrition.RemoveSlot slot.Id)
                    onPrescribe = slot |> prescribeOf props.nutrition props.planChanging held
                    offers = offers
                    busy = busy
                |}

        let slots = slotsShown |> List.map slotOf |> List.toArray

        let feedingAddButton =
            Components.AddButton.View
                {|
                    label = Terms.``Nutrition Enteral Feeding`` |> getTerm "Enterale Voeding"
                    onClick = fun () -> props.nutrition.StartSlot NutritionCategory.EnteralFeeding
                    disabled = held
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
