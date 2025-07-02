namespace Views


module ContinuousMeds =

    open Fable.Core
    open Feliz
    open Shared


    [<JSX.Component>]
    let View (props : {| interventions: Deferred<Types.Intervention list>; localizationTerms : Deferred<string [] []>; onSelectItem: string -> unit  |}) =

        let context = React.useContext Global.context
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
            {|  field = "indication"; headerName = Terms.``Continuous Medication Indication`` |> getTerm "Indicatie"; width = 200; filterable = true; sortable = true |}
            {|  field = "medication"; headerName = Terms.``Continuous Medication Medication`` |> getTerm "Medicatie"; width = 200; filterable = true; sortable = true |}
            {|  field = "quantity"; headerName = Terms.``Continuous Medication Quantity`` |> getTerm "Hoeveelheid"; width = 150; filterable = false; sortable = false |}
            {|  field = "solution"; headerName = Terms.``Continuous Medication Solution`` |> getTerm "Oplossing"; width = 150; filterable = false; sortable = false |} //``type`` = "number"
            {|  field = "dose"; headerName = Terms.``Continuous Medication Dose`` |> getTerm "Dosering"; width = 230; filterable = false; sortable = false |} //``type`` = "number"
            {|  field = "advice"; headerName = Terms.``Continuous Medication Advice`` |> getTerm "Advies"; width = 190; filterable = false; sortable = false |}
        |]

        let rows =
            match props.interventions with
            | Resolved items ->
                items
                |> List.filter (fun i ->
                    hosp |> String.isNullOrWhiteSpace ||
                    i.Hospital |> String.isNullOrWhiteSpace ||
                    hosp = i.Hospital
                )
                |> List.toArray
                |> Array.mapi (fun i m ->
                    {|
                        cells =
                            [|
                                {| field = "id"; value = $"{i + 1}.{m.Name}" |}
                                {| field = "indication"; value = $"{m.Indication}" |}
                                {| field = "medication"; value = $"**{m.Name}**" |}
                                {| field = "quantity"; value = $"{m.Quantity} {m.QuantityUnit}" |}
                                {| field = "solution"; value = $"{m.Total} ml {m.Solution}" |}
                                {| field = "dose"; value = $"{m.SubstanceDoseText}" |}
                                {| field = "advice"; value = m.Text |}
                            |]
                        actions = None
                    |}
                )
            | _ -> [||]

        let rowCreate (cells : string []) =
            if cells |> Array.length <> 7 then
                failwith $"cannot create row with {cells}"
            else
                {|
                    id = cells[0]
                    indication = cells[1].Replace("*", "")
                    medication = cells[2].Replace("*", "")
                    quantity = cells[3].Replace("*", "")
                    solution = cells[4].Replace("*", "")
                    dose = cells[5].Replace("*", "")
                    advice = cells[6].Replace("*", "")
                |}
            |> box

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';

        <Box sx={ {| height="100%" |} } >
            {
                Components.ResponsiveTable.View({|
                    hideFilter = false
                    columns = columns
                    rows = rows
                    rowCreate = rowCreate
                    height = "70vh"
                    onRowClick = props.onSelectItem
                    checkboxSelection = false
                    selectedRows = [||]
                    onSelectChange = ignore
                |})
            }
        </Box>
        """
