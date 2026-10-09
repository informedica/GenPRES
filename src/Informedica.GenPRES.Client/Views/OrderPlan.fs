namespace Views


module OrderPlan =


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models
    open OrderPlanMachine
    open OrderContextMachine


    /// A plan table cell that steps its order: the text, and a quantity field in a popper
    /// anchored to the cell. The cell is a button: a click, Enter or Space asks to open or close
    /// it, and the popper has its own close button. Which cell is open is the plan view's, so
    /// one is open at a time. A click or a key inside reaches no row, so the order dialog does
    /// not open. The popper stays mounted while closed, so a step still counting its clicks is
    /// sent after the close.
    [<JSX.Component>]
    let PlanCell
        (props:
            {|
                text: string
                field: JSX.Element
                closeLabel: string
                isOpen: bool
                // the cell cannot be opened now: greyed, and only an open popper can be closed
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

        // the context that contributes the order, by order id
        let contextOf (tp: OrderPlan) (orderId: string) =
            tp.OrderContexts
            |> Array.tryFind (fun c -> OrderContext.contribution c |> Option.exists (fun sc -> sc.Order.Id = orderId))

        // an order-context command into the selected context, sent over the plan held
        let orderContextMsg cmd =
            match orderPlan with
            | OrderPlanView.Settled(_, Some id)
            | OrderPlanView.Changing(_, Some id) -> envOrderPlan.OrderDialogCommand(id, cmd)
            | OrderPlanView.Settled(_, None)
            | OrderPlanView.Changing(_, None)
            | OrderPlanView.NoPatient -> ()

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization

        // the dialog is open while a context is selected: the selection is the state, and the
        // selected context as the plan shows it, settled or changing as the plan is, is what
        // the dialog shows
        let dialog = OrderContextView.dialog orderPlan
        let handleModalClose = fun () -> envOrderPlan.SelectContext None


        let getTerm = Global.getLocalizedTerm localizationTerms lang

        // the sheet's translation in the User's language, else the policy's English
        let tr term =
            Global.getLocalizedTerm localizationTerms lang (SigningPolicy.english term) term

        // the orders of the contexts whose patient data differ from the plan's: locked, and
        // marked in the medication cell
        let lockedOrders =
            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) ->
                tp.OrderContexts
                |> Array.filter (PlanContextPolicy.locked tp)
                |> Array.choose (OrderContext.contribution >> Option.map _.Order.Id)
                |> Set.ofArray
            | OrderPlanView.NoPatient -> Set.empty

        let lockedText =
            Terms.``Plan Context Locked``
            |> getTerm "Berekend met andere patiëntgegevens; deze order kan niet worden aangepast"

        let lockSx =
            {|
                display = "flex"
                alignItems = "center"
                gap = 1
            |}

        // the medication, with the lock and its reason on hover when the order is locked
        // the medication name reads as a link, since a click on it opens the order: blue, bold,
        // underlined under the pointer, after the icon of the prescribe page's edit button
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
            // a locked order carries the lock in place of the edit icon: it opens read-only
            if lockedOrders |> Set.contains id then
                JSX.jsx
                    $"""
                    import Box from '@mui/material/Box';
                    import Tooltip from '@mui/material/Tooltip';

                    <Tooltip title={lockedText}>
                        <Box sx={lockSx}>{Mui.Icons.LockIcon}<Box sx={medicationSx}>{value}</Box></Box>
                    </Tooltip>
                    """
            else
                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';

                <Box sx={medicationSx}>{Mui.Icons.CalculateIcon}{value}</Box>
                """

        // the cell whose field is open, one at a time, by row id and column
        let openCell, setOpenCell = React.useState<string option> None

        // the cells step only while the plan is settled and no signature is under way
        let cellsRest =
            match orderPlan with
            | OrderPlanView.Settled _ -> SigningPolicy.underWay signing.Signing
            | OrderPlanView.Changing _
            | OrderPlanView.NoPatient -> true

        let fieldTexts = ViewHelpers.quantityFieldTexts getTerm

        // the quantity field a cell steps, its commands sent into the row's own context, as the
        // order dialog builds the same field
        let cellField (tp: OrderPlan) (ctx: OrderContext) (ord: Order) (field, ovar: OrderVariable) =
            let send cmd = envOrderPlan.OrderDialogCommand(ctx.Id, cmd)

            let stepable = QuantityModePolicy.Mode.Stepable

            let mode, label =
                match field with
                | QuantityModePolicy.Field.Frequency ->
                    ViewHelpers.frequencyStepper
                        send
                        stepable
                        Api.OrderViewCommand.SetMinScheduleFrequencyProperty
                        Api.OrderViewCommand.DecreaseScheduleFrequencyProperty
                        Api.OrderViewCommand.SetMedianScheduleFrequencyProperty
                        Api.OrderViewCommand.IncreaseScheduleFrequencyProperty
                        Api.OrderViewCommand.SetMaxScheduleFrequencyProperty,
                    Terms.``Order Frequency`` |> getTerm "frequentie"
                | QuantityModePolicy.Field.DoseQuantity ->
                    ViewHelpers.createDoseQtyStepper
                        send
                        ord
                        Api.OrderViewCommand.SetMinOrderableDoseQuantityProperty
                        Api.OrderViewCommand.DecreaseOrderableDoseQuantityProperty
                        Api.OrderViewCommand.SetMedianOrderableDoseQuantityProperty
                        Api.OrderViewCommand.IncreaseOrderableDoseQuantityProperty
                        Api.OrderViewCommand.SetMaxOrderableDoseQuantityProperty,
                    "toedien hoeveelheid"
                | QuantityModePolicy.Field.DoseRate ->
                    ViewHelpers.doseRateStepper
                        send
                        stepable
                        ovar
                        Api.OrderViewCommand.SetMinOrderableDoseRateProperty
                        Api.OrderViewCommand.DecreaseOrderableDoseRateProperty
                        Api.OrderViewCommand.SetMedianOrderableDoseRateProperty
                        Api.OrderViewCommand.IncreaseOrderableDoseRateProperty
                        Api.OrderViewCommand.SetMaxOrderableDoseRateProperty,
                    Terms.``Order Drip rate`` |> getTerm "inloop snelheid"
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

        // a cell of a column that can step: the quantity field in a popper when the plan cell
        // rule says the cell steps, the text otherwise
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
                | OrderPlanView.NoPatient -> None

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
                    headerName = Terms.``Continuous Medication Medication`` |> getTerm "Medicatie"
                    width = 200
                    filterable = true
                    sortable = true
                    renderCell = renderMedicationCell
                    renderCard = renderMedicationCell
                |}
                |> box
                {|
                    field = "route"
                    headerName = Terms.Route |> getTerm "Route"
                    width = 150
                    filterable = true
                    sortable = true
                |}
                |> box
                {|
                    field = "frequency"
                    headerName = Terms.``Order Plan Frequency`` |> getTerm "Frequentie"
                    width = 150
                    filterable = false
                    sortable = false
                    renderCell = renderFrequencyCell
                    renderCard = renderFrequencyCell
                |}
                |> box
                {|
                    field = "quantity"
                    headerName = Terms.``Continuous Medication Quantity`` |> getTerm "Hoeveelheid"
                    width = 150
                    filterable = false
                    sortable = false
                |}
                |> box
                {|
                    field = "solution"
                    headerName = Terms.``Continuous Medication Solution`` |> getTerm "Oplossing"
                    width = 150
                    filterable = false
                    sortable = false
                    renderCell = renderSolutionCell
                    renderCard = renderSolutionCell
                |}
                |> box //``type`` = "number"
                {|
                    field = "dose"
                    headerName = Terms.``Continuous Medication Dose`` |> getTerm "Dosering"
                    width = 200
                    filterable = false
                    sortable = false
                |}
                |> box //``type`` = "number"
            |]

        let rows =
            let parseVals = Order.Variable.renderValues 3

            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) ->
                OrderPlan.orders tp
                |> Array.map _.Order
                |> Array.mapi (fun i o ->
                    let freq =
                        if o.Schedule.IsDiscontinuous || o.Schedule.IsTimed then
                            o.Schedule.Frequency.Variable |> Order.Variable.renderValue 3
                        else if o.Schedule.IsContinuous then
                            o.Orderable.Dose.Rate.Variable |> Order.Variable.renderValue 3
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
                            |> Option.map (fun i -> i.OrderableQuantity.Variable |> Order.Variable.renderValue 3)
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
                            o.Orderable.Dose.Quantity.Variable |> Order.Variable.renderValue 3
                        else if o.Schedule.IsContinuous then
                            o.Orderable.OrderableQuantity.Variable |> Order.Variable.renderValue 3
                        else
                            ""

                    let dose =
                        if o.Schedule.IsDiscontinuous || o.Schedule.IsTimed then
                            itms |> Array.map _.Dose.PerTimeAdjust.Variable |> parseVals
                        else if o.Schedule.IsContinuous then
                            itms
                            |> Array.tryHead
                            |> Option.map (fun i -> i.Dose.RateAdjust.Variable |> Order.Variable.renderValue 3)
                            |> Option.defaultValue ""

                        else if o.Schedule.IsOnce || o.Schedule.IsOnceTimed then
                            itms |> Array.map _.Dose.QuantityAdjust.Variable |> parseVals
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
            | OrderPlanView.NoPatient -> [||]

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

        // a row clicked: its order's context becomes the selection
        let selectOrder id =
            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) ->
                match contextOf tp id with
                | None -> Logging.error "Order not found" id
                | Some c -> envOrderPlan.SelectContext(Some c.Id)
            | OrderPlanView.NoPatient -> ()

        // the rows checked, by order id, become the filter, by context id; only over a plan at
        // rest, as the filter is sent
        let filterOrders ids =
            match orderPlan with
            | OrderPlanView.Settled(tp, _) ->
                ids
                |> Array.choose (fun id -> contextOf tp id |> Option.map _.Id)
                |> envOrderPlan.FilterRows
            | OrderPlanView.NoPatient
            | OrderPlanView.Changing _ -> ()

        // the orders of the contexts the filter keeps, for the table's checked rows
        let selectedRows =
            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) when tp.Filtered |> Array.isEmpty |> not ->
                OrderPlan.filtered tp
                |> Array.choose OrderContext.contribution
                |> Array.map _.Order.Id
            | _ -> [||]

        // removing is asked first: the button opens the question, confirming does it
        let confirmDeleteOpen, setConfirmDeleteOpen = React.useState false

        let onDelete = fun () -> setConfirmDeleteOpen true

        // the contexts the filter keeps go, each with its order
        let onDeleteConfirmed =
            fun () ->
                match orderPlan with
                | OrderPlanView.Settled(tp, _) -> envOrderPlan.Remove tp.Filtered
                | OrderPlanView.NoPatient
                | OrderPlanView.Changing _ -> ()

                setConfirmDeleteOpen false

        let confirmDeleteDialog =
            Components.ConfirmDialog.View
                {|
                    isOpen = confirmDeleteOpen
                    title = "Verwijder Geselecteerde Voorschriften"
                    text = "De geselecteerde voorschriften worden uit het order plan verwijderd. Wilt u doorgaan?"
                    confirmLabel = Terms.Delete |> getTerm "Verwijderen"
                    cancelLabel = Terms.Cancel |> getTerm "Annuleren"
                    onConfirm = onDeleteConfirmed
                    onCancel = fun () -> setConfirmDeleteOpen false
                |}

        // a clear from a field's arrow goes as a reopen of the plan, which keeps the plan before it
        let reopen cmd =
            match orderPlan with
            | OrderPlanView.Settled(_, Some id)
            | OrderPlanView.Changing(_, Some id) -> envOrderPlan.ReopenField(id, cmd)
            | OrderPlanView.Settled(_, None)
            | OrderPlanView.Changing(_, None)
            | OrderPlanView.NoPatient -> ()

        let orderContext = dialog |> Option.defaultValue OrderContextView.NoPatient

        let nothingSelected =
            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) -> tp.Filtered |> Array.isEmpty
            | OrderPlanView.NoPatient -> true

        // always there with a plan, disabled without a selection, so the row above the table
        // keeps its height and the table does not move when a row is checked
        let deleteAction =
            {|
                label = Terms.Delete |> getTerm "Verwijderen"
                kind = Components.ActionBar.Kind.Destructive
                onClick = onDelete
                disabled = nothingSelected
                icon = Some Mui.Icons.Delete
            |}

        // a Prescriber with an open Session signs the plan as shown
        let onSign =
            fun _ ->
                match orderPlan with
                | OrderPlanView.Settled(tp, _) -> signing.Sign tp
                | OrderPlanView.NoPatient
                | OrderPlanView.Changing _ -> ()

        // the button stays while the plan changes, disabled, so the table below does not move up
        // and down with every answer
        let signAction =
            {|
                label = tr Terms.``Signing Sign``
                kind = Components.ActionBar.Kind.Primary
                onClick = fun () -> onSign ()
                disabled = false
                icon = Some Mui.Icons.Assignment
            |}

        // the last signed order plan opened again; the new and changed orders go with it. Shown
        // only once there is one; disabled with the page, also while a step is counted, whose
        // command would go to the plan being replaced
        let openLastSignedAction =
            HeldPanelPolicy.lastSigned session.Session
            |> Option.map (fun signed ->
                {|
                    label = tr Terms.``Session Open Last Signed``
                    kind = Components.ActionBar.Kind.Secondary
                    onClick = fun () -> session.OpenSignedPlan signed.Head.Id
                    disabled = false
                    icon = Some Mui.Icons.Restore
                |}
            )

        // the actions on one row: remove on the left, sign on the right
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
                                if SigningPolicy.canSign session.Session tp then
                                    signAction
                            |]
                    |}
            | OrderPlanView.NoPatient -> null

        // the record moved on while this Session is on an older version; the bar says whose
        // and when, and offers the newest version. Nothing is blocked here: the guard is the
        // refusal at the signature
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
                                        // the open changes every page: it waits while anything is out
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

        let signDialog = SignDialog.View {| appEnv = props.appEnv |}

        let responsiveTable =
            Components.ResponsiveTable.View
                {|
                    hideFilter = true
                    columns = columns
                    rows = rows
                    rowCreate = rowCreate
                    height = "100%"
                    onRowClick = selectOrder
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
                |}

        // the order dialog under the plan context rule: the fields it allows, or nothing when
        // the context is locked
        let editing =
            match orderPlan with
            | OrderPlanView.Settled(tp, Some id)
            | OrderPlanView.Changing(tp, Some id) -> PlanContextPolicy.editingOf tp id
            | OrderPlanView.Settled(_, None)
            | OrderPlanView.Changing(_, None)
            | OrderPlanView.NoPatient -> PlanContextPolicy.Editing.PlanContext

        let orderView =
            Order.View
                {|
                    editing = editing
                    orderContext = orderContext
                    command = orderContextMsg
                    reopen = reopen
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
                    needs = PatientReadiness.Needs.Calculation
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
