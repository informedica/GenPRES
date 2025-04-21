namespace Views


module TreatmentPlan =


    open Fable.Core
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models


    [<JSX.Component>]
    let View (props : {|
        treatmentPlan: Deferred<TreatmentPlan>
        updateTreatmentPlan: TreatmentPlan -> unit
        filterTreatmentPlan: TreatmentPlan -> unit
        localizationTerms : Deferred<string [] []>
        |}) =

        let context = React.useContext Global.context
        let lang = context.Localization

        let modalOpen, setModalOpen = React.useState false
        let handleModalClose =
            fun () ->
                match props.treatmentPlan with
                | Resolved tp ->
                    { tp with Selected = None }
                    |> props.updateTreatmentPlan
                | _ -> ()
                setModalOpen false

        let getTerm defVal term =
            props.localizationTerms
            |> Deferred.map (fun terms ->
                Localization.getTerm terms lang term
                |> Option.defaultValue defVal
            )
            |> Deferred.defaultValue defVal

        let columns = [|
            {|  field = "id"; headerName = "id"; width = 0; filterable = false; sortable = false |}
            {|  field = "medication"; headerName = Terms.``Continuous Medication Medication`` |> getTerm "Medicatie"; width = 200; filterable = true; sortable = true |}
            {|  field = "route"; headerName = "Route"; width = 150; filterable = true; sortable = true |}
            {|  field = "frequency"; headerName = "Frequentie"; width = 150; filterable = false; sortable = false |}
            {|  field = "quantity"; headerName = Terms.``Continuous Medication Quantity`` |> getTerm "Hoeveelheid"; width = 150; filterable = false; sortable = false |}
            {|  field = "solution"; headerName = Terms.``Continuous Medication Solution`` |> getTerm "Oplossing"; width = 150; filterable = false; sortable = false |} //``type`` = "number"
            {|  field = "dose"; headerName = Terms.``Continuous Medication Dose`` |> getTerm "Dosering"; width = 200; filterable = false; sortable = false |} //``type`` = "number"
        |]

        let getVal (vals : ValueUnit option) =
            match vals with
            | Some v ->
                match v.Value with
                | [| (_, s) |] ->
                    let s = s |> float |> Utils.Math.fixPrecision 3
                    $"{s} {v.Unit}"
                | _ -> ""
            | None -> ""

        let rows =
            let parseVals vals =
                vals
                |> Array.map getVal
                |> Array.map (String.split " ")
                |> Array.groupBy Array.tryLast
                |> Array.map (fun (k, v) ->
                    match k with
                    | Some u ->
                        v
                        |> Array.collect id
                        |> Array.map (String.replace u "")
                        |> Array.map _.Trim()
                        |> Array.filter (String.isNullOrWhiteSpace >> not)
                        |> String.concat "/"
                        |> fun s -> $"{s} {u}"
                    | None -> ""
                )
                |> String.concat ""

            match props.treatmentPlan with
            | Resolved tp ->
                tp.Scenarios
                |> Array.map _.Order
                |> Array.mapi (fun i o ->
                    let freq =
                        if o.Prescription.IsDiscontinuous || o.Prescription.IsTimed then
                            o.Prescription.Frequency.Variable.Vals |> getVal
                        else if o.Prescription.IsContinuous then
                            o.Orderable.Dose.Rate.Variable.Vals |> getVal
                        else ""

                    let itms =
                        match o.Orderable.Components |> Array.tryHead with
                        | Some c ->
                            c.Items
                            |> Array.filter (_.IsAdditional >> not)
                        | _ -> [||]

                    let qty =
                        if o.Prescription.IsDiscontinuous ||
                           o.Prescription.IsTimed ||
                           o.Prescription.IsOnce ||
                           o.Prescription.IsOnceTimed then
                            itms
                            |> Array.map _.Dose.Quantity.Variable.Vals
                            |> parseVals
                        else if o.Prescription.IsContinuous then
                            itms
                            |> Array.tryHead
                            |> Option.map (fun i -> i.OrderableQuantity.Variable.Vals |> getVal)
                            |> Option.defaultValue ""
                        else ""

                    let sol =
                        if o.Prescription.IsDiscontinuous ||
                           o.Prescription.IsTimed ||
                           o.Prescription.IsOnce ||
                           o.Prescription.IsOnceTimed
                            then
                            o.Orderable.Dose.Quantity.Variable.Vals |> getVal
                        else if o.Prescription.IsContinuous then
                            o.Orderable.OrderableQuantity.Variable.Vals |> getVal
                        else ""

                    let dose =
                        if o.Prescription.IsDiscontinuous || o.Prescription.IsTimed then
                            itms
                            |> Array.map _.Dose.PerTimeAdjust.Variable.Vals
                            |> parseVals
                        else if o.Prescription.IsContinuous then
                            itms
                            |> Array.tryHead
                            |> Option.map (fun i -> i.Dose.RateAdjust.Variable.Vals |> getVal)
                            |> Option.defaultValue ""

                        else if o.Prescription.IsOnce || o.Prescription.IsOnceTimed then
                            itms
                            |> Array.map _.Dose.QuantityAdjust.Variable.Vals
                            |> parseVals
                        else ""

                    {|
                        cells =
                            [|
                                {| field = "id"; value = $"{o.Id}" |}
                                {| field = "medication"; value = $"{o.Orderable.Name}" |}
                                {| field = "route"; value = $"{o.Route}" |}
                                {| field = "frequency"; value = $"{freq}" |}
                                {| field = "quantity"; value = $"{qty}" |}
                                {| field = "solution"; value = $"{sol}" |}
                                {| field = "dose"; value = $"{dose}" |}
                            |]
                        actions = None
                    |}
                )
            | _ -> [||]

        let rowCreate (cells : string []) =
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

        let modalStyle =
            {|
                position="absolute"
                top= "50%"
                left= "50%"
                transform= "translate(-50%, -50%)"
                width= 400
                bgcolor= "background.paper"
                boxShadow= 24
            |}

        let selectOrder id =
            match props.treatmentPlan with
            | Resolved tp ->
                tp.Scenarios
                |> Array.tryFind (fun sc -> sc.Order.Id = id)
                |> function
                | None ->
                    Logging.error "Order not found" id
                    ()
                | Some sc ->
                    { tp with Filtered = [||]; Selected = Some sc }
                    |> props.updateTreatmentPlan

                    setModalOpen true
            | _ -> ()

        let filterOrders ids =
            match props.treatmentPlan with
            | Resolved tp ->
                { tp with
                    Selected = None
                    Filtered =
                        if ids |> Array.isEmpty then [||]
                        else
                            tp.Scenarios
                            |> Array.filter (fun os ->
                                os.Order
                                |> _.Id
                                |> fun id -> ids |> Array.exists ((=) id)
                            )
                }
                |> props.filterTreatmentPlan
            | _ -> ()

        let selectedRows =
            match props.treatmentPlan with
            | Resolved tp ->
                tp.Filtered
                |> Array.map _.Order
                |> Array.map _.Id
            | _ -> [||]

        let onDelete =
            fun () ->
                match props.treatmentPlan with
                | Resolved tp ->
                    { tp with
                        Scenarios =
                            tp.Scenarios
                            |> Array.filter (fun sc ->
                                tp.Filtered
                                |> Array.exists ((=) sc)
                                |> not
                            )

                    }
                    |> props.updateTreatmentPlan
                | _ -> ()

        let updateOrderScenario (ctx : OrderContext) =
            match props.treatmentPlan with
            | Resolved tp ->
                match ctx.Scenarios |> Array.tryExactlyOne with
                | None -> ()
                | Some os ->
                    { tp with
                        Selected = Some os
                        Scenarios =
                            tp.Scenarios
                            |> Array.map (fun sc ->
                                if sc |> OrderScenario.eqs os then os else sc
                            )

                    }
                    |> props.updateTreatmentPlan
            | _ -> ()

        let refreshOrderScenario (ctx : OrderContext) =
            match props.treatmentPlan with
            | Resolved tp ->
                match ctx.Scenarios |> Array.tryExactlyOne with
                | None -> ()
                | Some os ->
                    { tp with
                        Selected = Some os
                        Scenarios =
                            tp.Scenarios
                            |> Array.map (fun sc ->
                                if sc |> OrderScenario.eqs os then os else sc
                            )

                    }
                    |> props.updateTreatmentPlan
            | _ -> ()

        let deleteBtn =
            match props.treatmentPlan with
            | Resolved tp when tp.Filtered |> Array.length > 0 ->
                JSX.jsx $"""
                import Button from '@mui/material/Button';

                <Box sx={ {| mt=2 |} }>
                    <Button variant="text" onClick={onDelete} fullWidth startIcon={Mui.Icons.Delete} >
                        Verwijder Geselecteerde Voorschriften
                    </Button>
                </Box>
                """
            | _ -> JSX.jsx $"<></>"


        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Modal from '@mui/material/Modal';

        <Box sx={ {| height="100%" |} } >
            {deleteBtn}
            {
                Components.ResponsiveTable.View({|
                    hideFilter = true
                    columns = columns
                    rows = rows
                    rowCreate = rowCreate
                    height = "50vh"
                    onRowClick = selectOrder
                    checkboxSelection = true
                    selectedRows = selectedRows
                    onSelectChange = filterOrders
                |})
            }
            <Modal open={modalOpen} onClose={handleModalClose} >
                <Box sx={modalStyle}>
                    {
                        Order.View {|
                            orderContext =
                                match props.treatmentPlan with
                                | Resolved tp ->
                                    tp.Selected
                                    |> Option.map (fun sc ->
                                        OrderContext.fromOrderScenario tp.Patient sc
                                        |> Resolved
                                    )
                                    |> Option.defaultValue HasNotStartedYet
                                | _ -> HasNotStartedYet
                            updateOrderScenario = updateOrderScenario
                            refreshOrderScenario = refreshOrderScenario
                            closeOrder = handleModalClose
                            localizationTerms = props.localizationTerms
                        |}
                    }
                </Box>
            </Modal>
        </Box>
        """
