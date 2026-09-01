namespace Views


module OrderPlan =


    open Fable.Core
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envOrderPlan = AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv
        let orderPlan = envOrderPlan.OrderPlan
        let orderPlanCommand = envOrderPlan.OrderPlanCommand

        let updateOrderPlan tp =
            orderPlanCommand (Api.UpdateOrderPlan(tp, None))

        let filterOrderPlan tp =
            orderPlanCommand (Api.FilterOrderPlan tp)

        let orderContextMsg (cmd, ctx) =
            match orderPlan with
            | Resolved tp
            | Recalculating tp -> orderPlanCommand (Api.UpdateOrderPlan(tp, Some(cmd, ctx)))
            | _ -> ()

        let localizationTerms =
            (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization

        // Derive modal visibility from Elmish state — if an order is selected, the modal is open.
        // This avoids duplicating tp.Selected.IsSome in local React state.
        let modalOpen =
            match orderPlan with
            | Resolved tp
            | Recalculating tp -> tp.Selected.IsSome
            | _ -> false

        let handleModalClose =
            fun () ->
                match orderPlan with
                | Resolved tp
                | Recalculating tp -> { tp with Selected = None } |> updateOrderPlan
                | _ -> ()


        let getTerm = Global.getLocalizedTerm localizationTerms lang

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
            | Resolved tp
            | Recalculating tp ->
                tp.Scenarios
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
            | _ -> [||]

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

        let selectOrder id =
            match orderPlan with
            | Resolved tp
            | Recalculating tp ->
                tp.Scenarios
                |> Array.tryFind (fun sc -> sc.Order.Id = id)
                |> function
                    | None ->
                        Logging.error "Order not found" id
                        ()
                    | Some sc ->
                        { tp with
                            Filtered = [||]
                            Selected = Some sc
                        }
                        |> updateOrderPlan
            | _ -> ()

        let filterOrders ids =
            match orderPlan with
            | Resolved tp
            | Recalculating tp ->
                { tp with
                    Selected = None
                    Filtered =
                        if ids |> Array.isEmpty then
                            [||]
                        else
                            tp.Scenarios
                            |> Array.filter (fun os -> os.Order |> _.Id |> (fun id -> ids |> Array.exists ((=) id)))
                }
                |> filterOrderPlan
            | _ -> ()

        let selectedRows =
            match orderPlan with
            | Resolved tp
            | Recalculating tp -> tp.Filtered |> Array.map _.Order |> Array.map _.Id
            | _ -> [||]

        let onDelete =
            fun () ->
                match orderPlan with
                | Resolved tp
                | Recalculating tp ->
                    { tp with
                        Scenarios =
                            tp.Scenarios
                            |> Array.filter (fun sc -> tp.Filtered |> Array.exists ((=) sc) |> not)

                    }
                    |> updateOrderPlan
                | _ -> ()

        let updateOrderScenario (ctx: OrderContext) =
            orderContextMsg (Api.UpdateOrderScenario, ctx)

        let refreshOrderScenario (ctx: OrderContext) =
            orderContextMsg (Api.ResetOrderScenario, ctx)

        let stepOrderScenario =
            {|
                // Frequency
                setMinFrequency = fun ctx -> orderContextMsg (Api.SetMinScheduleFrequencyProperty, ctx)
                decrFrequency = fun ctx -> orderContextMsg (Api.DecreaseScheduleFrequencyProperty, ctx)
                setMedianFrequency = fun ctx -> orderContextMsg (Api.SetMedianScheduleFrequencyProperty, ctx)
                incrFrequency = fun ctx -> orderContextMsg (Api.IncreaseScheduleFrequencyProperty, ctx)
                setMaxFrequency = fun ctx -> orderContextMsg (Api.SetMaxScheduleFrequencyProperty, ctx)
                // Rate
                setMinRate = fun ctx -> orderContextMsg (Api.SetMinOrderableDoseRateProperty, ctx)
                decrRate = fun (ctx, n, uc) -> orderContextMsg (Api.DecreaseOrderableDoseRateProperty(n, uc), ctx)
                setMedianRate = fun ctx -> orderContextMsg (Api.SetMedianOrderableDoseRateProperty, ctx)
                incrRate = fun (ctx, n, uc) -> orderContextMsg (Api.IncreaseOrderableDoseRateProperty(n, uc), ctx)
                setMaxRate = fun ctx -> orderContextMsg (Api.SetMaxOrderableDoseRateProperty, ctx)
                // Dose Quantity
                setMinDoseQty = fun ctx -> orderContextMsg (Api.SetMinOrderableDoseQuantityProperty, ctx)
                decrDoseQty =
                    fun (ctx, n, uc) -> orderContextMsg (Api.DecreaseOrderableDoseQuantityProperty(n, uc), ctx)
                setMedianDoseQty = fun ctx -> orderContextMsg (Api.SetMedianOrderableDoseQuantityProperty, ctx)
                incrDoseQty =
                    fun (ctx, n, uc) -> orderContextMsg (Api.IncreaseOrderableDoseQuantityProperty(n, uc), ctx)
                setMaxDoseQty = fun ctx -> orderContextMsg (Api.SetMaxOrderableDoseQuantityProperty, ctx)
                // Component Quantity
                setMinComponentQty =
                    fun (ctx, cmp) -> orderContextMsg (Api.SetMinComponentOrderableQuantityProperty cmp, ctx)
                decrComponentQty =
                    fun (ctx, cmp, n, uc) ->
                        orderContextMsg (Api.DecreaseComponentOrderableQuantityProperty(cmp, n, uc), ctx)
                setMedianComponentQty =
                    fun (ctx, cmp) -> orderContextMsg (Api.SetMedianComponentOrderableQuantityProperty cmp, ctx)
                incrComponentQty =
                    fun (ctx, cmp, n, uc) ->
                        orderContextMsg (Api.IncreaseComponentOrderableQuantityProperty(cmp, n, uc), ctx)
                setMaxComponentQty =
                    fun (ctx, cmp) -> orderContextMsg (Api.SetMaxComponentOrderableQuantityProperty cmp, ctx)
            |}

        let orderContext =
            match orderPlan with
            | Resolved tp ->
                tp.Selected
                |> Option.map (fun sc -> OrderContext.fromOrderScenario tp.Patient sc |> Resolved)
                |> Option.defaultValue HasNotStartedYet
            | Recalculating tp ->
                tp.Selected
                |> Option.map (fun sc -> OrderContext.fromOrderScenario tp.Patient sc |> Recalculating)
                |> Option.defaultValue HasNotStartedYet
            | _ -> HasNotStartedYet

        let deleteBtn =
            match orderPlan with
            | Resolved tp
            | Recalculating tp when tp.Filtered |> Array.length > 0 ->
                JSX.jsx
                    $"""
                import Button from '@mui/material/Button';

                <Box sx={ {| marginTop = 2 |} }>
                    <Button variant="text" onClick={onDelete} fullWidth startIcon={Mui.Icons.Delete} >
                        Verwijder Geselecteerde Voorschriften
                    </Button>
                </Box>
                """
            | _ -> null

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
            {deleteBtn}
            {responsiveTable}
            <Modal open={modalOpen} onClose={handleModalClose} >
                <Box sx={modalStyle}>
                    {orderView}
                </Box>
            </Modal>
        </Box>
        """
