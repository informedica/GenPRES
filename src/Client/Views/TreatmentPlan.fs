namespace Views


module TreatmentPlan =

    open Fable.Core
    open Feliz

    open Shared


    [<JSX.Component>]
    let View (props : {|
        loadOrder: (string option * Order) -> unit
        order: Deferred<(bool * string option * Order) option>
        removeOrderFromPlan : Order -> unit
        updateScenarioOrder : unit -> unit
        treatmentPlan: Deferred<(Scenario * Order) []>
        selectOrder : (Types.Scenario * Types.Order option) -> unit
        localizationTerms : Deferred<string [] []>
        |}) =

        let context = React.useContext Global.context
        let lang = context.Localization

        let modalOpen, setModalOpen = React.useState false
        let handleModalClose = fun () -> setModalOpen false

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
            {|  field = "frequency"; headerName = "frequentie"; width = 150; filterable = false; sortable = false |}
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
            match props.treatmentPlan with
            | Resolved ords ->
                ords
                |> Array.mapi (fun i (b, o) ->
                    let freq =
                        if o.Prescription.IsDiscontinuous || o.Prescription.IsTimed then
                            o.Prescription.Frequency.Variable.Vals |> getVal
                        else if o.Prescription.IsContinuous then
                            o.Orderable.Dose.Rate.Variable.Vals |> getVal
                        else ""

                    let itm =
                        match o.Orderable.Components |> Array.tryHead with
                        | Some c ->
                            c.Items
                            |> Array.filter (_.IsAdditional >> not)
                            |> Array.tryHead
                        | _ -> None

                    let qty =
                        if o.Prescription.IsDiscontinuous ||
                           o.Prescription.IsTimed ||
                           o.Prescription.IsOnce ||
                           o.Prescription.IsOnceTimed then
                            itm
                            |> Option.map (fun i -> i.Dose.Quantity.Variable.Vals |> getVal)
                            |> Option.defaultValue ""
                        else if o.Prescription.IsContinuous then
                            itm
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
                            itm
                            |> Option.map (fun i -> i.Dose.PerTimeAdjust.Variable.Vals |> getVal)
                            |> Option.defaultValue ""
                        else if o.Prescription.IsContinuous then
                            itm
                            |> Option.map (fun i -> i.Dose.RateAdjust.Variable.Vals |> getVal)
                            |> Option.defaultValue ""

                        else if o.Prescription.IsOnce || o.Prescription.IsOnceTimed then
                            itm
                            |> Option.map (fun i -> i.Dose.QuantityAdjust.Variable.Vals |> getVal)
                            |> Option.defaultValue ""
                        else ""

                    {|
                        cells =
                            [|
                                {| field = "id"; value = $"{o.Id}" |}
                                {| field = "medication"; value = $"{o.Orderable.Name}" |}
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
                medication = cells[1].Replace("*", "")
                frequency = cells[2].Replace("*", "")
                quantity = cells[3].Replace("*", "")
                solution = cells[4].Replace("*", "")
                dose = cells[5].Replace("*", "")
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

        let selectOrder s =
            match props.treatmentPlan with
            | Resolved tp ->
                tp
                |> Array.tryFind (fun (_, o) -> o.Id = s)
                |> function
                | None -> ()
                | Some (sc, ord) -> setModalOpen true; (sc, Some ord) |> props.selectOrder
            | _ -> ()

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Modal from '@mui/material/Modal';

        <Box sx={ {| height="100%" |} } >
            {
                Components.ResponsiveTable.View({|
                    hideFilter = true
                    columns = columns
                    rows = rows
                    rowCreate = rowCreate
                    height = "50vh"
                    onRowClick = selectOrder
                |})
            }
            <Modal open={modalOpen} onClose={handleModalClose} >
                <Box sx={modalStyle}>
                    {
                        Order.View {|
                            order = props.order
                            loadOrder = props.loadOrder
                            updateScenarioOrder = props.updateScenarioOrder
                            closeOrder = handleModalClose
                            localizationTerms = props.localizationTerms
                        |}
                    }
                </Box>
            </Modal>
        </Box>
        """
