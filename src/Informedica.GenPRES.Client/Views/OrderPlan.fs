namespace Views


module OrderPlan =


    open Fable.Core
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models
    open OrderPlanMachine
    open OrderContextMachine


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envOrderPlan = AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv
        let orderPlan = envOrderPlan.OrderPlan
        let planCommand = envOrderPlan.OrderPlanCommand
        let session = AppEnv.asEnv<AppEnv.ISession> props.appEnv
        let signing = AppEnv.asEnv<AppEnv.ISigning> props.appEnv

        // the context that contributes the order, by order id
        let contextOf (tp: OrderPlan) (orderId: string) =
            tp.OrderContexts
            |> Array.tryFind (fun c -> OrderContext.contribution c |> Option.exists (fun sc -> sc.Order.Id = orderId))

        // an order-context command into the selected context: a step, settled or changing as
        // the plan is; while changing it waits in the lane for the answer
        let orderContextMsg (cmd, ctx) =
            match orderPlan with
            | OrderPlanView.Settled(tp, Some id)
            | OrderPlanView.Changing(tp, Some id) -> planCommand (Api.OrderPlanCommand.Navigate(tp, id, cmd, ctx))
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
        let handleModalClose = fun () -> envOrderPlan.Select None


        let getTerm = Global.getLocalizedTerm localizationTerms lang

        // the sheet's translation in the User's language, else the policy's English
        let tr term =
            Global.getLocalizedTerm localizationTerms lang (SigningPolicy.english term) term

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
                    headerName = Terms.``Order Frequency`` |> getTerm "Frequentie"
                    width = 150
                    filterable = false
                    sortable = false
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

        let modalStyle = ViewHelpers.modalStyle

        // a row clicked: its order's context becomes the selection
        let selectOrder id =
            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) ->
                match contextOf tp id with
                | None -> Logging.error "Order not found" id
                | Some c -> envOrderPlan.Select(Some c.Id)
            | OrderPlanView.NoPatient -> ()

        // the rows checked, by order id, become the filter, by context id; only over a plan at
        // rest, as the filter is sent
        let filterOrders ids =
            match orderPlan with
            | OrderPlanView.Settled(tp, _) ->
                ids
                |> Array.choose (fun id -> contextOf tp id |> Option.map _.Id)
                |> envOrderPlan.Filter
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

        // a plan change is one at a time: while one is under way the button is disabled,
        // so a click never sends a command that would be discarded
        let isRecalculating =
            match orderPlan with
            | OrderPlanView.Changing _ -> true
            | OrderPlanView.NoPatient
            | OrderPlanView.Settled _ -> false

        // removing is asked first: the button opens the question, confirming does it
        let confirmDeleteOpen, setConfirmDeleteOpen = React.useState false

        let onDelete = fun () -> setConfirmDeleteOpen true

        // the contexts the filter keeps go, each with its order
        let onDeleteConfirmed =
            fun () ->
                match orderPlan with
                | OrderPlanView.Settled(tp, _) ->
                    planCommand (Api.OrderPlanCommand.RemoveOrderContexts(tp, tp.Filtered))
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

        let updateOrderScenario (ctx: OrderContext) =
            orderContextMsg (Api.OrderContextCommand.UpdateOrderScenario, ctx)

        let refreshOrderScenario (ctx: OrderContext) =
            orderContextMsg (Api.OrderContextCommand.ResetOrderScenario, ctx)

        let stepOrderScenario =
            {|
                // Frequency
                setMinFrequency =
                    fun ctx -> orderContextMsg (Api.OrderContextCommand.SetMinScheduleFrequencyProperty, ctx)
                decrFrequency =
                    fun ctx -> orderContextMsg (Api.OrderContextCommand.DecreaseScheduleFrequencyProperty, ctx)
                setMedianFrequency =
                    fun ctx -> orderContextMsg (Api.OrderContextCommand.SetMedianScheduleFrequencyProperty, ctx)
                incrFrequency =
                    fun ctx -> orderContextMsg (Api.OrderContextCommand.IncreaseScheduleFrequencyProperty, ctx)
                setMaxFrequency =
                    fun ctx -> orderContextMsg (Api.OrderContextCommand.SetMaxScheduleFrequencyProperty, ctx)
                // Rate
                setMinRate = fun ctx -> orderContextMsg (Api.OrderContextCommand.SetMinOrderableDoseRateProperty, ctx)
                decrRate =
                    fun (ctx, n, uc) ->
                        orderContextMsg (Api.OrderContextCommand.DecreaseOrderableDoseRateProperty(n, uc), ctx)
                setMedianRate =
                    fun ctx -> orderContextMsg (Api.OrderContextCommand.SetMedianOrderableDoseRateProperty, ctx)
                incrRate =
                    fun (ctx, n, uc) ->
                        orderContextMsg (Api.OrderContextCommand.IncreaseOrderableDoseRateProperty(n, uc), ctx)
                setMaxRate = fun ctx -> orderContextMsg (Api.OrderContextCommand.SetMaxOrderableDoseRateProperty, ctx)
                // Dose Quantity
                setMinDoseQty =
                    fun ctx -> orderContextMsg (Api.OrderContextCommand.SetMinOrderableDoseQuantityProperty, ctx)
                decrDoseQty =
                    fun (ctx, n, uc) ->
                        orderContextMsg (Api.OrderContextCommand.DecreaseOrderableDoseQuantityProperty(n, uc), ctx)
                setMedianDoseQty =
                    fun ctx -> orderContextMsg (Api.OrderContextCommand.SetMedianOrderableDoseQuantityProperty, ctx)
                incrDoseQty =
                    fun (ctx, n, uc) ->
                        orderContextMsg (Api.OrderContextCommand.IncreaseOrderableDoseQuantityProperty(n, uc), ctx)
                setMaxDoseQty =
                    fun ctx -> orderContextMsg (Api.OrderContextCommand.SetMaxOrderableDoseQuantityProperty, ctx)
                // Component Quantity
                setMinComponentQty =
                    fun (ctx, cmp) ->
                        orderContextMsg (Api.OrderContextCommand.SetMinComponentOrderableQuantityProperty cmp, ctx)
                decrComponentQty =
                    fun (ctx, cmp, n, uc) ->
                        orderContextMsg (
                            Api.OrderContextCommand.DecreaseComponentOrderableQuantityProperty(cmp, n, uc),
                            ctx
                        )
                setMedianComponentQty =
                    fun (ctx, cmp) ->
                        orderContextMsg (Api.OrderContextCommand.SetMedianComponentOrderableQuantityProperty cmp, ctx)
                incrComponentQty =
                    fun (ctx, cmp, n, uc) ->
                        orderContextMsg (
                            Api.OrderContextCommand.IncreaseComponentOrderableQuantityProperty(cmp, n, uc),
                            ctx
                        )
                setMaxComponentQty =
                    fun (ctx, cmp) ->
                        orderContextMsg (Api.OrderContextCommand.SetMaxComponentOrderableQuantityProperty cmp, ctx)
            |}

        let orderContext = dialog |> Option.defaultValue OrderContextView.NoPatient

        let deleteBtn =
            match orderPlan with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) when tp.Filtered |> Array.length > 0 ->
                JSX.jsx
                    $"""
                import Button from '@mui/material/Button';

                <Box sx={ {| marginTop = 2 |} }>
                    <Button variant="text" onClick={onDelete} disabled={isRecalculating} fullWidth startIcon={Mui.Icons.Delete} >
                        Verwijder Geselecteerde Voorschriften
                    </Button>
                </Box>
                """
            | _ -> null

        // a Prescriber with an open Session signs the plan as shown
        let onSign =
            fun _ ->
                match orderPlan with
                | OrderPlanView.Settled(tp, _) -> signing.Sign tp
                | OrderPlanView.NoPatient
                | OrderPlanView.Changing _ -> ()

        let signBtn =
            match orderPlan with
            | OrderPlanView.Settled(tp, _) when SigningPolicy.canSign session.Session tp ->
                JSX.jsx
                    $"""
                import Button from '@mui/material/Button';

                <Box sx={ {| marginTop = 2 |} }>
                    <Button variant="contained" onClick={onSign} startIcon={Mui.Icons.Assignment} >
                        {tr Terms.``Signing Sign``}
                    </Button>
                </Box>
                """
            | _ -> null

        // the record moved on while this Session is on an older version; the bar says whose
        // and when, and offers the newest version. Nothing is blocked here: the guard is the
        // refusal at the signature
        let movedOnBar =
            match session.MovedOn with
            | Some head ->
                let onOpenNewest = fun _ -> session.OpenVersion head.Id

                let openNewest =
                    JSX.jsx
                        $"""
                    import Button from '@mui/material/Button';

                    <Button color="inherit" size="small" onClick={onOpenNewest}>
                        {tr Terms.``Session Open Newest``}
                    </Button>
                    """

                JSX.jsx
                    $"""
                import Alert from '@mui/material/Alert';

                <Box sx={ {| marginTop = 2 |} }>
                    <Alert severity="warning" action={openNewest}>
                        {SigningPolicy.movedOnSentence tr head}
                    </Alert>
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
                    // one change to the plan at a time: a checkbox toggled while it is busy
                    // would be dropped, so the boxes are greyed meanwhile
                    selectDisabled = isRecalculating
                    selectedRows = selectedRows
                    onSelectChange = filterOrders
                    showToolbar = true
                    showFooter = true
                    onPrint = None
                    selectedFilter = None
                    onFilterChange = None
                    // no column filter on the plan, so no label to show
                    filterLabel = ""
                |}

        let orderView =
            Order.View
                {|
                    orderContext = orderContext
                    updateOrderScenario = updateOrderScenario
                    stepOrderScenario = stepOrderScenario
                    refreshOrderScenario = refreshOrderScenario
                    closeOrder = handleModalClose
                    localizationTerms = localizationTerms
                |}

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Modal from '@mui/material/Modal';

        <Box sx={ {| height = "100%" |} }>
            {movedOnBar}{signBtn}
            {deleteBtn}
            {responsiveTable}
            <Modal open={dialog.IsSome} onClose={handleModalClose} >
                <Box sx={modalStyle}>
                    {orderView}
                </Box>
            </Modal>
            {confirmDeleteDialog}
            {signDialog}
        </Box>
        """
