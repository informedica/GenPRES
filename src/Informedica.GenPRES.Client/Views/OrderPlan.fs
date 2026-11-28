namespace Views


module OrderPlan =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines
    open Informedica.GenPRES.Client.Core.MachinePolicies


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open OrderPlanMachine
    open OrderContextMachine


    /// A plan table cell that steps its order: shows the text and a quantity field in a popper anchored to
    /// the cell. A click, Enter or Space opens or closes the popper, which also has its own close button.
    /// The plan view decides which cell is open, so only one is open at a time. Clicks and keys inside the
    /// cell do not reach the row, so the order dialog does not open. The popper stays mounted while closed,
    /// so a step that still counts clicks is sent after the close.
    [<JSX.Component>]
    let PlanCell
        (props:
            {|
                text: string
                field: JSX.Element
                closeLabel: string
                isOpen: bool
                // True when the cell cannot be opened now: it is grey, and only an open popper can be closed.
                disabled: bool
                onToggle: unit -> unit
                onClose: unit -> unit
            |})
        =
        let anchor, setAnchor = React.useState<Browser.Types.Element option> None

        let toggle (e: Browser.Types.Event) =
            e.stopPropagation ()

            if not props.disabled || props.isOpen then
                setAnchor (Some(e.currentTarget :?> Browser.Types.Element))
                props.onToggle ()

        let onKey (e: Browser.Types.KeyboardEvent) =
            if e.key = "Enter" || e.key = " " then
                e.preventDefault ()
                toggle e

        let close (e: Browser.Types.Event) =
            e.stopPropagation ()
            props.onClose ()

        let stop (e: Browser.Types.Event) = e.stopPropagation ()

        let isOpen = props.isOpen && anchor.IsSome
        let anchorEl = anchor |> Option.map box |> Option.defaultValue null

        // a cell that can be stepped reads as a control: a light blue box with a pencil, darker
        // under the pointer and while its field is open; grey while it cannot be opened
        let border, background, text, icon =
            if props.disabled then
                Mui.Colors.Grey.``300``, Mui.Colors.Grey.``100``, Mui.Colors.Grey.``600``, Mui.Colors.Grey.``500``
            elif isOpen then
                Mui.Colors.Blue.``700``, Mui.Colors.Blue.``100``, Mui.Colors.Blue.``900``, Mui.Colors.Blue.``700``
            else
                Mui.Colors.Blue.``200``, Mui.Colors.Blue.``50``, Mui.Colors.Blue.``900``, Mui.Colors.Blue.``700``

        let hover =
            if props.disabled then
                {|
                    borderColor = border
                    backgroundColor = background
                |}
            else
                {|
                    borderColor = Mui.Colors.Blue.``700``
                    backgroundColor = Mui.Colors.Blue.``100``
                |}

        let cellSx =
            {|
                display = "inline-flex"
                alignItems = "center"
                gap = 0.5
                paddingX = 0.75
                paddingY = 0.25
                borderRadius = 1
                border = $"1px solid %s{border}"
                backgroundColor = background
                color = text
                cursor = (if props.disabled then "default" else "pointer")
                ``& svg`` =
                    {|
                        fontSize = 16
                        color = icon
                    |}
                ``&:hover`` = hover
            |}

        let popperSx = {| zIndex = 1300 |}

        let paperSx =
            {|
                display = "flex"
                alignItems = "flex-start"
                gap = 0.5
                padding = 1
            |}

        let tabIndex = if props.disabled && not isOpen then -1 else 0

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import IconButton from '@mui/material/IconButton';
        import Paper from '@mui/material/Paper';
        import Popper from '@mui/material/Popper';

        <Box
            sx={cellSx}
            role="button"
            aria-expanded={isOpen}
            aria-disabled={props.disabled}
            tabIndex={tabIndex}
            onClick={toggle}
            onKeyDown={onKey}
        >
            {props.text}{Mui.Icons.Edit}
            <Popper open={isOpen} anchorEl={anchorEl} placement="bottom-start" keepMounted={true} sx={popperSx}>
                <Paper elevation={4} sx={paperSx} onClick={stop} onKeyDown={stop}>
                    {props.field}
                    <IconButton size="small" aria-label={props.closeLabel} title={props.closeLabel} onClick={close}>
                        {Mui.Icons.Close}
                    </IconButton>
                </Paper>
            </Popper>
        </Box>
        """


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envOrderPlan = AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv
        let orderPlan = envOrderPlan.OrderPlan
        let session = AppEnv.asEnv<AppEnv.ISession> props.appEnv
        let signing = AppEnv.asEnv<AppEnv.ISigning> props.appEnv

        // Finds the context whose scenario contributes the order with this id.
        let contextOf (tp: OrderPlan) (orderId: string) =
            tp.OrderContexts
            |> Array.tryFind (fun c -> OrderContext.contribution c |> Option.exists (fun sc -> sc.Order.Id = orderId))

        // Sends an order context command to the selected context; does nothing without a selection.
        let orderContextMsg cmd =
            match orderPlan with
            | OrderPlanView.Settled(_, Some id)
            | OrderPlanView.Changing(_, Some id) -> envOrderPlan.OrderDialogCommand(id, cmd)
            | OrderPlanView.Settled(_, None)
            | OrderPlanView.Changing(_, None)
            | OrderPlanView.NoPatientData -> ()

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization

        // The dialog is open while a context is selected. It shows the selected context as the plan shows
        // it, settled or changing.
        let dialog = OrderContextView.dialog orderPlan
        let handleModalClose = fun () -> envOrderPlan.SelectContext None


        let getTerm = Global.getLocalizedTerm localizationTerms lang

        // Translates a term into the language of the user, with the English of the signing policy as fallback.
        let tr term =
            Global.getLocalizedTerm localizationTerms lang (SigningPolicy.english term) term

        // Finds the plan and the context an order comes from, by order id.
        let planContextOf id =
            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) -> contextOf tp id |> Option.map (fun ctx -> tp, ctx)
            | OrderPlanView.NoPatientData -> None

        let lockSx =
            {|
                display = "flex"
                alignItems = "center"
                gap = 1
            |}

        // The medication name looks like a link, because a click opens the order: blue, bold and underlined
        // under the pointer, after the icon of the edit button on the prescribe page.
        let medicationSx =
            {|
                display = "inline-flex"
                alignItems = "center"
                color = Mui.Colors.Blue.``800``
                fontWeight = 600
                gap = 1
                ``& svg`` = {| fontSize = 18 |}
                ``&:hover`` = {| textDecoration = "underline" |}
            |}

        let medicationCell (id: string) (value: string) =
            let planContext = planContextOf id

            // a linked order is marked with an arrow, under its main order
            let value =
                match planContext |> Option.bind (fun (_, ctx) -> ctx.LinkedTo) with
                | Some _ -> $"↳ %s{value}"
                | None -> value

            let lock =
                planContext
                |> Option.map (fun (tp, ctx) -> PlanContextPolicy.lock tp ctx)
                |> Option.defaultValue PlanContextPolicy.Lock.Unlocked

            // A locked order shows a lock icon instead of the edit icon, with the reason in a tooltip; it opens
            // read-only.
            match lock |> PlanContextPolicy.lockTerm with
            | Some term ->
                let lockedText = tr term

                JSX.jsx
                    $"""
                    import Box from '@mui/material/Box';
                    import Tooltip from '@mui/material/Tooltip';

                    <Tooltip title={lockedText}>
                        <Box sx={lockSx}>{Mui.Icons.LockIcon}<Box sx={medicationSx}>{value}</Box></Box>
                    </Tooltip>
                    """
            | None ->
                JSX.jsx
                    $"""
                    import Box from '@mui/material/Box';

                    <Box sx={medicationSx}>{Mui.Icons.CalculateIcon}{value}</Box>
                    """

        // The key of the open cell, made of the row id and the column; at most one cell is open.
        let openCell, setOpenCell = React.useState<string option> None

        // True when the cells cannot step: the plan is changing or a signature is under way.
        let cellsRest =
            match orderPlan with
            | OrderPlanView.Settled _ -> SigningPolicy.underWay signing.Signing
            | OrderPlanView.Changing _
            | OrderPlanView.NoPatientData -> true

        let fieldTexts = ViewHelpers.quantityFieldTexts getTerm

        // The quantity field of a cell, built as in the order dialog; its commands go to the context of the row.
        let cellField (tp: OrderPlan) (ctx: OrderContext) (ord: Order) (field, ovar: OrderVariable) =
            let evaluate cmd = envOrderPlan.OrderDialogCommand(ctx.Id, cmd)

            let stepable = QuantityModePolicy.Mode.Stepable

            let mode, label =
                match field with
                | QuantityModePolicy.Field.Frequency ->
                    ViewHelpers.frequencyStepper
                        evaluate
                        stepable
                        Api.OrderViewCommand.SetMinScheduleFrequencyProperty
                        Api.OrderViewCommand.DecreaseScheduleFrequencyProperty
                        Api.OrderViewCommand.SetMedianScheduleFrequencyProperty
                        Api.OrderViewCommand.IncreaseScheduleFrequencyProperty
                        Api.OrderViewCommand.SetMaxScheduleFrequencyProperty,
                    Terms.``Order Frequency`` |> getTerm "frequency"
                | QuantityModePolicy.Field.DoseQuantity ->
                    ViewHelpers.createDoseQtyStepper
                        evaluate
                        ord
                        Api.OrderViewCommand.SetMinOrderableDoseQuantityProperty
                        Api.OrderViewCommand.DecreaseOrderableDoseQuantityProperty
                        Api.OrderViewCommand.SetMedianOrderableDoseQuantityProperty
                        Api.OrderViewCommand.IncreaseOrderableDoseQuantityProperty
                        Api.OrderViewCommand.SetMaxOrderableDoseQuantityProperty,
                    Terms.``Order Dose Quantity`` |> getTerm "dose quantity"
                | QuantityModePolicy.Field.DoseRate ->
                    ViewHelpers.doseRateStepper
                        evaluate
                        stepable
                        ovar
                        Api.OrderViewCommand.SetMinOrderableDoseRateProperty
                        Api.OrderViewCommand.DecreaseOrderableDoseRateProperty
                        Api.OrderViewCommand.SetMedianOrderableDoseRateProperty
                        Api.OrderViewCommand.IncreaseOrderableDoseRateProperty
                        Api.OrderViewCommand.SetMaxOrderableDoseRateProperty,
                    Terms.``Order Drip rate`` |> getTerm "drip rate"
                | QuantityModePolicy.Field.ComponentQuantity
                | QuantityModePolicy.Field.Other -> Components.QuantityField.Fixed, ""

            ovar
            |> ViewHelpers.ovarValsWithRange string 3
            |> ViewHelpers.orderFixed
                fieldTexts
                false
                cellsRest
                false
                label
                None
                ignore
                mode
                (ovar |> ViewHelpers.markOf)
                None

        // A cell of a column that can step: the quantity field in a popper when the plan cell policy lets
        // the cell step, else the text.
        let steppingCell (column: PlanCellPolicy.Column) (id: string) (value: string) =
            let stepping =
                match orderPlan with
                | OrderPlanView.Settled(tp, _)
                | OrderPlanView.Changing(tp, _) ->
                    contextOf tp id
                    |> Option.bind (fun ctx ->
                        PlanCellPolicy.stepable tp ctx column
                        |> Option.bind (fun fieldOf ->
                            OrderContext.contribution ctx
                            |> Option.map (fun sc -> cellField tp ctx sc.Order fieldOf)
                        )
                    )
                | OrderPlanView.NoPatientData -> None

            let key = $"%s{id}/%A{column}"

            match stepping with
            | Some field ->
                PlanCell
                    {|
                        text = value
                        field = field
                        closeLabel = "sluiten"
                        isOpen = openCell = Some key
                        disabled = cellsRest
                        onToggle = fun () -> setOpenCell (if openCell = Some key then None else Some key)
                        onClose = fun () -> setOpenCell None
                    |}
            | None -> Html.text value |> toJsx

        // one renderer for both layouts: the grid and the cards pass the row id and the value
        let rendered (cell: string -> string -> JSX.Element) =
            fun (pars: obj) -> cell (pars?id: string) (pars?value: string)

        let renderMedicationCell = rendered medicationCell
        let renderFrequencyCell = rendered (steppingCell PlanCellPolicy.Column.Frequency)
        let renderSolutionCell = rendered (steppingCell PlanCellPolicy.Column.Solution)

        let columns =
            [|
                {|
                    field = "id"
                    headerName = "id"
                    width = 0
                    filterable = false
                    sortable = false
                |}
                |> box
                {|
                    field = "medication"
                    headerName = Terms.``Continuous Medication Medication`` |> getTerm "Medication"
                    width = 200
                    filterable = true
                    // no sort, which would part a linked order from its main order
                    sortable = false
                    renderCell = renderMedicationCell
                    renderCard = renderMedicationCell
                |}
                |> box
                {|
                    field = "route"
                    headerName = Terms.Route |> getTerm "Route"
                    width = 150
                    filterable = true
                    sortable = false
                |}
                |> box
                {|
                    field = "frequency"
                    headerName = Terms.``Order Plan Frequency`` |> getTerm "Frequency"
                    width = 150
                    filterable = false
                    sortable = false
                    renderCell = renderFrequencyCell
                    renderCard = renderFrequencyCell
                |}
                |> box
                {|
                    field = "quantity"
                    headerName = Terms.``Continuous Medication Quantity`` |> getTerm "Quantity"
                    width = 150
                    filterable = false
                    sortable = false
                |}
                |> box
                {|
                    field = "solution"
                    headerName = Terms.``Continuous Medication Solution`` |> getTerm "Solution"
                    width = 150
                    filterable = false
                    sortable = false
                    renderCell = renderSolutionCell
                    renderCard = renderSolutionCell
                |}
                |> box //``type`` = "number"
                {|
                    field = "dose"
                    headerName = Terms.``Continuous Medication Dose`` |> getTerm "Dose"
                    width = 200
                    filterable = false
                    sortable = false
                |}
                |> box //``type`` = "number"
            |]

        let rows =
            let parseVals = OrderDisplay.Variable.renderValues 3

            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) ->
                OrderPlan.orders tp
                |> Array.map _.Order
                |> Array.mapi (fun i o ->
                    let freq =
                        if o.Schedule.IsDiscontinuous || o.Schedule.IsTimed then
                            o.Schedule.Frequency.Variable |> OrderDisplay.Variable.renderValue 3
                        else if o.Schedule.IsContinuous then
                            o.Orderable.Dose.Rate.Variable |> OrderDisplay.Variable.renderValue 3
                        else
                            ""

                    let itms =
                        match o.Orderable.Components |> Array.tryHead with
                        | Some c -> c.Items |> Array.filter (_.IsAdditional >> not)
                        | _ -> [||]

                    let qty =
                        if
                            o.Schedule.IsDiscontinuous
                            || o.Schedule.IsTimed
                            || o.Schedule.IsOnce
                            || o.Schedule.IsOnceTimed
                        then
                            itms |> Array.map _.Dose.Quantity.Variable |> parseVals
                        else if o.Schedule.IsContinuous then
                            itms
                            |> Array.tryHead
                            |> Option.map (fun i ->
                                i.OrderableQuantity.Variable |> OrderDisplay.Variable.renderValue 3
                            )
                            |> Option.defaultValue ""
                        else
                            ""

                    let sol =
                        if
                            o.Schedule.IsDiscontinuous
                            || o.Schedule.IsTimed
                            || o.Schedule.IsOnce
                            || o.Schedule.IsOnceTimed
                        then
                            o.Orderable.Dose.Quantity.Variable |> OrderDisplay.Variable.renderValue 3
                        else if o.Schedule.IsContinuous then
                            o.Orderable.OrderableQuantity.Variable |> OrderDisplay.Variable.renderValue 3
                        else
                            ""

                    // An order without a per kg or per m2 limit leaves the adjusted dose empty, so the dose
                    // itself is shown.
                    let adjustedOr adj dose =
                        if adj |> OrderDisplay.Variable.renderValue 3 = "" then
                            dose
                        else
                            adj

                    let dose =
                        if o.Schedule.IsDiscontinuous || o.Schedule.IsTimed then
                            itms
                            |> Array.map (fun i -> adjustedOr i.Dose.PerTimeAdjust.Variable i.Dose.PerTime.Variable)
                            |> parseVals
                        else if o.Schedule.IsContinuous then
                            itms
                            |> Array.tryHead
                            |> Option.map (fun i ->
                                adjustedOr i.Dose.RateAdjust.Variable i.Dose.Rate.Variable
                                |> OrderDisplay.Variable.renderValue 3
                            )
                            |> Option.defaultValue ""

                        else if o.Schedule.IsOnce || o.Schedule.IsOnceTimed then
                            itms
                            |> Array.map (fun i -> adjustedOr i.Dose.QuantityAdjust.Variable i.Dose.Quantity.Variable)
                            |> parseVals
                        else
                            ""

                    {|
                        cells =
                            [|
                                {|
                                    field = "id"
                                    value = $"{o.Id}"
                                |}
                                {|
                                    field = "medication"
                                    value = $"{o.Orderable.Name}"
                                |}
                                {|
                                    field = "route"
                                    value = $"{o.Route}"
                                |}
                                {|
                                    field = "frequency"
                                    value = $"{freq}"
                                |}
                                {|
                                    field = "quantity"
                                    value = $"{qty}"
                                |}
                                {|
                                    field = "solution"
                                    value = $"{sol}"
                                |}
                                {|
                                    field = "dose"
                                    value = $"{dose}"
                                |}
                            |]
                        actions = None
                    |}
                )
            | OrderPlanView.NoPatientData -> [||]

        let rowCreate (cells: string[]) =
            {|
                id = cells[0]
                medication = cells[1]
                route = cells[2]
                frequency = cells[3]
                quantity = cells[4]
                solution = cells[5]
                dose = cells[6]
            |}
            |> box

        // A clicked row selects the context of its order.
        let selectOrder id =
            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) ->
                match contextOf tp id with
                | None -> Logging.error "Order not found" id
                | Some c -> envOrderPlan.SelectContext(Some c.Id)
            | OrderPlanView.NoPatientData -> ()

        // The checked rows, by order id, become the filter by context id; a main order and its linked orders
        // are checked together. Only for a settled plan, because the filter is sent to the server.
        let filterOrders ids =
            match orderPlan with
            | OrderPlanView.Settled(tp, _) ->
                ids
                |> Array.choose (fun id -> contextOf tp id |> Option.map _.Id)
                |> PlanRowPolicy.checkedRows tp
                |> envOrderPlan.FilterRows
            | OrderPlanView.NoPatientData
            | OrderPlanView.Changing _ -> ()

        // The order ids of the contexts in the filter, for the checked rows of the table.
        let selectedRows =
            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) when tp.Filtered |> Array.isEmpty |> not ->
                OrderPlan.filtered tp
                |> Array.choose OrderContext.contribution
                |> Array.map _.Order.Id
            | _ -> [||]

        // Removing asks first: the button opens the confirm dialog, and confirming removes.
        let confirmDeleteOpen, setConfirmDeleteOpen = React.useState false

        let onDelete = fun () -> setConfirmDeleteOpen true

        // Removes the contexts in the filter, each with its order.
        let onDeleteConfirmed =
            fun () ->
                match orderPlan with
                | OrderPlanView.Settled(tp, _) -> envOrderPlan.Remove tp.Filtered
                | OrderPlanView.NoPatientData
                | OrderPlanView.Changing _ -> ()

                setConfirmDeleteOpen false

        let confirmDeleteDialog =
            Components.ConfirmDialog.View
                {|
                    isOpen = confirmDeleteOpen
                    title = "Verwijder Geselecteerde Voorschriften"
                    text = "De geselecteerde voorschriften worden uit het order plan verwijderd. Wilt u doorgaan?"
                    confirmLabel = Terms.Delete |> getTerm "Delete"
                    cancelLabel = Terms.Cancel |> getTerm "Cancel"
                    onConfirm = onDeleteConfirmed
                    onCancel = fun () -> setConfirmDeleteOpen false
                |}

        let orderContext = dialog |> Option.defaultValue OrderContextView.NoPatientData

        let nothingSelected =
            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) -> tp.Filtered |> Array.isEmpty
            | OrderPlanView.NoPatientData -> true

        // always there with a plan, disabled without a selection, so the row above the table
        // keeps its height and the table does not move when a row is checked
        let deleteAction =
            {|
                label = Terms.Delete |> getTerm "Delete"
                kind = Components.ActionBar.Kind.Destructive
                onClick = onDelete
                disabled = nothingSelected
                icon = Some Mui.Icons.Delete
            |}

        // Signs the plan as shown; does nothing while the plan is changing.
        let onSign =
            fun _ ->
                match orderPlan with
                | OrderPlanView.Settled(tp, _) -> signing.Sign tp
                | OrderPlanView.NoPatientData
                | OrderPlanView.Changing _ -> ()

        // The sign button is never disabled; a click while the plan is changing does nothing.
        let signAction =
            {|
                label = tr Terms.``Signing Sign``
                kind = Components.ActionBar.Kind.Primary
                onClick = fun () -> onSign ()
                disabled = false
                icon = Some Mui.Icons.Assignment
            |}

        // Opens the last signed order plan again. Shown only when the session has a signed plan.
        let openLastSignedAction =
            SessionPatientPolicy.lastSigned session.Session
            |> Option.map (fun signed ->
                {|
                    label = tr Terms.``Session Open Last Signed``
                    kind = Components.ActionBar.Kind.Secondary
                    onClick = fun () -> session.OpenSignedPlan signed.Head.Id
                    disabled = false
                    icon = Some Mui.Icons.Restore
                |}
            )

        // The actions in one row: remove, open the last signed plan, and sign.
        let actionBar =
            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) ->
                Components.ActionBar.View
                    {|
                        actions =
                            [|
                                deleteAction
                                yield! openLastSignedAction |> Option.toArray
                                if
                                    SigningPolicy.canSign session.Session (envOrderPlan.PlanDifferences tp) tp
                                    && not envOrderPlan.PlanBehind
                                then
                                    signAction
                            |]
                    |}
            | OrderPlanView.NoPatientData -> null

        // The record has a newer plan than the one this session shows. The bar says who signed it and when,
        // and offers to open it. Nothing is blocked here; signing an older version is refused.
        let newerPlanBar =
            match session.NewerPlan with
            | Some head ->
                let bar =
                    Components.Notice.View
                        {|
                            kind = Components.Notice.Kind.Warning
                            title = None
                            message = SigningPolicy.newerPlanSentence tr head
                            action =
                                Some
                                    {|
                                        label = tr Terms.``Session Open Newest``
                                        onClick = fun () -> session.OpenSignedPlan head.Id
                                        // Opening changes every page, so it waits while any request is out.
                                        disabled = (AppEnv.asEnv<AppEnv.IBusy> props.appEnv).Any
                                    |}
                            onClose = None
                        |}

                JSX.jsx
                    $"""
                <Box sx={ {| marginTop = 2 |} }>
                    {bar}
                </Box>
                """
            | None -> null

        // The editing of the order dialog under the plan context policy: the fields it allows, or nothing
        // when the context is locked.
        let editing =
            match orderPlan with
            | OrderPlanView.Settled(tp, Some id)
            | OrderPlanView.Changing(tp, Some id) -> PlanContextPolicy.editingOf tp id
            | OrderPlanView.Settled(_, None)
            | OrderPlanView.Changing(_, None)
            | OrderPlanView.NoPatientData -> PlanContextPolicy.Editing.PlanContext PlanContextPolicy.DoseShare.Fixed

        let signDialog = SignDialog.View {| appEnv = props.appEnv |}

        // the order whose dialog is open, by its id as the table's rows carry it
        let markedRow =
            dialog
            |> Option.bind OrderContextView.context
            |> Option.bind OrderContext.contribution
            |> Option.map _.Order.Id

        let responsiveTable =
            Components.ResponsiveTable.View
                {|
                    hideFilter = true
                    columns = columns
                    rows = rows
                    rowCreate = rowCreate
                    height = "100%"
                    onRowClick = selectOrder
                    markedRow = markedRow
                    checkboxSelection = true
                    selectedRows = selectedRows
                    onSelectChange = filterOrders
                    showToolbar = true
                    showFooter = true
                    onPrint = None
                    selectedFilter = None
                    onFilterChange = None
                    // no column filter on the plan, so no label to show
                    filterLabel = ""
                    searchLabel = ""
                    actions = None
                |}

        let orderView =
            Order.View
                {|
                    editing = editing
                    orderContext = orderContext
                    command = orderContextMsg
                    restoreOrderScenario = envOrderPlan.RestoreField
                    closeOrder = handleModalClose
                    busy = (AppEnv.asEnv<AppEnv.IBusy> props.appEnv).Page Global.Pages.OrderPlan
                    localizationTerms = localizationTerms
                |}

        let orderDialog = if dialog.IsSome then orderView else null

        // the bars above the table keep their height and the table takes what is left, so the
        // grid's footer stays above the totals bar instead of being pushed below the page
        let sxPlan =
            {|
                height = "100%"
                display = "flex"
                flexDirection = "column"
            |}

        let sxBars = {| flexShrink = 0 |}

        let patientNotice =
            Components.PatientNotice.View
                {|
                    appEnv = props.appEnv
                    needs = PatientReadinessPolicy.Needs.Calculation
                |}

        let sxTable =
            {|
                flex = 1
                minHeight = 0
            |}

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';

        <Box sx={sxPlan}>
            <Box sx={sxBars}>
                {patientNotice}
                {newerPlanBar}
                {actionBar}
            </Box>
            <Box sx={sxTable}>
                {responsiveTable}
            </Box>
            {orderDialog}
            {confirmDeleteDialog}
            {signDialog}
        </Box>
        """
