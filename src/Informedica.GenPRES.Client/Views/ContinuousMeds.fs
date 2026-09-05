namespace Views


module ContinuousMeds =

    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Shared
    open Shared.Types


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envContinuous = AppEnv.asEnv<AppEnv.IContinuousMedication> props.appEnv
        let interventions = envContinuous.ContinuousMedication
        let onSelectItem = envContinuous.OnSelectContinuousMedicationItem
        let filterState = envContinuous.ContinuousMedicationFilter
        let onFilterChange = envContinuous.OnContinuousMedicationFilterChange

        let localizationTerms =
            (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let patient = (AppEnv.asEnv<AppEnv.IPatient> props.appEnv).Patient

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let hosp = context.Hospital

        let printOpen, setPrintOpen = React.useState false
        let weightKg = ViewHelpers.PrintView.patientWeight patient

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let renderQuantityCell =
            fun (pars: obj) ->
                let value: string = pars?value
                value |> TextBlock.fromString |> Mui.TypoGraphy.fromTextBlock

        let renderSolutionCell =
            fun (pars: obj) ->
                let value: string = pars?value
                value |> TextBlock.fromString |> Mui.TypoGraphy.fromTextBlock

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
                    field = "catagory"
                    headerName = Terms.``Continuous Medication Catagory`` |> getTerm "Categorie"
                    width = 140
                    filterable = true
                    sortable = true
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
                createObj
                    [
                        "field" ==> "quantity"
                        "headerName"
                        ==> (Terms.``Continuous Medication Quantity`` |> getTerm "Hoeveelheid")
                        "width" ==> 140
                        "filterable" ==> false
                        "sortable" ==> false
                        "renderCell" ==> renderQuantityCell
                    ]
                createObj
                    [
                        "field" ==> "solution"
                        "headerName"
                        ==> (Terms.``Continuous Medication Solution`` |> getTerm "Oplossing")
                        "width" ==> 140
                        "filterable" ==> false
                        "sortable" ==> false
                        "renderCell" ==> renderSolutionCell
                    ]
                {|
                    field = "dose"
                    headerName = Terms.``Continuous Medication Dose`` |> getTerm "Dosering"
                    width = 200
                    filterable = false
                    sortable = false
                |}
                |> box //``type`` = "number"
                {|
                    field = "advice"
                    headerName = Terms.``Continuous Medication Advice`` |> getTerm "Advies"
                    width = 200
                    filterable = false
                    sortable = false
                |}
                |> box
            |]

        let rows =
            match interventions with
            | Resolved items ->
                items
                |> List.filter (fun i ->
                    hosp |> String.isNullOrWhiteSpace
                    || i.Hospital |> String.isNullOrWhiteSpace
                    || hosp = i.Hospital
                )
                |> List.toArray
                |> Array.mapi (fun i m ->
                    {|
                        cells =
                            [|
                                {|
                                    field = "id"
                                    value = $"{i + 1}.{m.Name}"
                                |}
                                {|
                                    field = "catagory"
                                    value = $"{m.Category}"
                                |}
                                {|
                                    field = "medication"
                                    value = $"**{m.Name}**"
                                |}
                                {|
                                    field = "quantity"
                                    value = $"{m.Quantity} {m.QuantityUnit}"
                                |}
                                {|
                                    field = "solution"
                                    value = $"{m.Total} ml {m.Solution}"
                                |}
                                {|
                                    field = "dose"
                                    value = $"{m.SubstanceDoseText}"
                                |}
                                {|
                                    field = "advice"
                                    value = m.Text
                                |}
                            |]
                        actions = None
                    |}
                )
            | _ -> [||]

        let rowCreate (cells: string[]) =
            if cells |> Array.length <> 7 then
                invalidArg (nameof cells) $"cannot create row with {cells}"
            else
                {|
                    id = cells[0]
                    catagory = cells[1].Replace("*", "")
                    medication = cells[2].Replace("*", "")
                    quantity = cells[3].Replace("*", "")
                    solution = cells[4].Replace("*", "")
                    dose = cells[5].Replace("*", "")
                    advice = cells[6].Replace("*", "")
                |}
            |> box

        let printData, setPrintData = React.useState [||]

        let makePrintTableRows data =
            data
            |> Array.map (fun
                              (r:
                                  {|
                                      cells:
                                          {|
                                              field: string
                                              value: string
                                          |}[]
                                      actions: ReactElement option
                                  |}) ->
                let row = r.cells |> Array.map (fun c -> c.field, c.value) |> Map.ofArray

                let get f =
                    row |> Map.tryFind f |> Option.defaultValue "" |> _.Replace("*", "")

                let id = get "id"
                let catagory = get "catagory"
                let medication = get "medication"
                let quantity = get "quantity"
                let solution = get "solution"
                let dose = get "dose"
                let advice = get "advice"

                JSX.jsx
                    $"""
                import TableRow from '@mui/material/TableRow';
                import TableCell from '@mui/material/TableCell';

                <TableRow key={id}>
                    <TableCell>{catagory}</TableCell>
                    <TableCell>{medication}</TableCell>
                    <TableCell>{quantity}</TableCell>
                    <TableCell>{solution}</TableCell>
                    <TableCell>{dose}</TableCell>
                    <TableCell>{advice}</TableCell>
                </TableRow>
                """
            )
            |> unbox<seq<ReactElement>>
            |> React.Fragment

        let patientHeader = ViewHelpers.PrintView.PatientHeader {| weightKg = weightKg |}
        let patientSignature = ViewHelpers.PrintView.PatientSignature()
        let printTableRows = makePrintTableRows printData

        let printContent =
            let printTableSx =
                {|
                    tableLayout = "fixed"
                    width = "100%"
                |}

            let hdrCell w =
                {|
                    fontWeight = "bold"
                    width = w
                |}

            let hdr15 = hdrCell "15%"
            let hdr20 = hdrCell "20%"

            JSX.jsx
                $"""
            import Table from '@mui/material/Table';
            import TableBody from '@mui/material/TableBody';
            import TableHead from '@mui/material/TableHead';
            import TableRow from '@mui/material/TableRow';
            import TableCell from '@mui/material/TableCell';

            <React.Fragment>
                {patientHeader}
                <Table size="small" sx={printTableSx}>
                    <TableHead>
                        <TableRow>
                            <TableCell sx={hdr15}>Categorie</TableCell>
                            <TableCell sx={hdr20}>Medicatie</TableCell>
                            <TableCell sx={hdr15}>Hoeveelheid</TableCell>
                            <TableCell sx={hdr15}>Oplossing</TableCell>
                            <TableCell sx={hdr20}>Dosering</TableCell>
                            <TableCell sx={hdr15}>Advies</TableCell>
                        </TableRow>
                    </TableHead>
                    <TableBody>
                        {printTableRows}
                    </TableBody>
                </Table>
                {patientSignature}
            </React.Fragment>
            """
            |> toReact

        let boxSx = {| height = "100%" |}

        let tableProps =
            {|
                hideFilter = false
                columns = columns
                rows = rows
                rowCreate = rowCreate
                height = "100%"
                onRowClick = onSelectItem
                checkboxSelection = false
                selectedRows = [||]
                onSelectChange = ignore
                showToolbar = true
                showFooter = true
                onPrint =
                    Some(fun filteredRows ->
                        setPrintData filteredRows
                        setPrintOpen true
                    )
                selectedFilter = Some filterState
                onFilterChange = Some onFilterChange
            |}

        let printDialogProps =
            {|
                isOpen = printOpen
                onClose = fun () -> setPrintOpen false
                title = Terms.``Continuous Medication List`` |> getTerm "Continue Medicatie"
                children = printContent
            |}

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import React from 'react';

        <Box sx={boxSx} >
            {Components.ResponsiveTable.View tableProps}
            {ViewHelpers.PrintView.PrintDialog printDialogProps}
        </Box>
        """
