namespace Views


module TreatmentPlan =

    open Fable.Core
    open Feliz

    open Shared


    [<JSX.Component>]
    let View (props : {| treatmentPlan: Deferred<(bool * Order) []>; localizationTerms : Deferred<string [] []> |}) =

        let context = React.useContext(Global.context)
        let lang = context.Localization
        let hosp = context.Hospital

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
                                {| field = "id"; value = $"{i + 1}" |}
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

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';

        <Box sx={ {| height="100%" |} } >
            {
                Components.ResponsiveTable.View({|
                    columns = columns
                    rows = rows
                    rowCreate = rowCreate
                    height = "50vh"
                |})
            }
        </Box>
        """
