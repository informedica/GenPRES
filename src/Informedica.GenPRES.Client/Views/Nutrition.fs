namespace Views

module Nutrion =


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    [<JSX.Component>]
    let View () =

        let handleChange = fun _ -> ()

        let tpn =
            let rows =
                [|
                    {| group = "eiwitten"; medication = "Samenstelling C"; quantity = "100 mL"; rate=""; percentage=50 |}
                    {| group = "eiwitten"; medication = "glucose 10%"; quantity = "80 mL"; rate=""; percentage=50 |}
                    {| group = "eiwitten"; medication = "NaCl 0,9%"; quantity = "10 mL"; rate=""; percentage=10 |}
                    {| group = "eiwitten"; medication = "KCl 7,4%"; quantity = "10 mL"; rate=""; percentage=15 |}
                    {| group = "eiwitten"; medication = "eiwitten totaal"; quantity = "200 mL"; rate="20 mL/uur"; percentage=100 |}

                    {| group = "vetten"; medication = "intralipid 20%"; quantity = "10 mL"; rate=""; percentage=0 |}
                    {| group = "vetten"; medication = "vitintra infant"; quantity = "5 mL"; rate=""; percentage=0 |}
                    {| group = "vetten"; medication = "soluvit"; quantity = "5 mL"; rate=""; percentage=0 |}
                    {| group = "vetten"; medication = "vetten totaal"; quantity = "20 mL"; rate="20 mL/uur"; percentage=0 |}

                |]
                |> Array.mapi (fun i m ->
                    {|
                        cells =
                            [|
                                {| field = "id"; value = $"{i + 1}" |}
                                {| field = "group"; value = $"{m.group}" |}
                                {| field = "medication"; value = $"{m.medication}" |}
                                {| field = "quantity"; value = $"{m.quantity}" |}
                                {| field = "rate"; value = $"{m.rate}" |}
                                {| field = "percentage"; value = $"{m.percentage}" |}
                            |]
                        actions = None
                    |}
                )

            let rowCreate (cells : string []) =
                let success, percentageValue = System.Double.TryParse(cells[5])
                let percentageValue = if success then percentageValue else 0.0

                {|
                    id = cells[0]
                    group = cells[1].Replace("*", "")
                    medication = cells[2].Replace("*", "")
                    quantity = cells[3].Replace("*", "")
                    rate = cells[4].Replace("*", "")
                    percentage = percentageValue
                |}
                |> box

            let renderPercentageCell =
                fun (pars: obj) ->
                    let value: float = pars?value
                    let rowId: string = pars?id

                    let updatePercentage (newValue: float) =
                        printfn $"Row {rowId}: Percentage updated to: {newValue}"
                        // TODO: Add logic to update the row data

                    Components.Slider.View({|
                        label = ""
                        value = value
                        min = 0.0
                        max = 100.0
                        step = 5.0
                        updateValue = updatePercentage
                        isLoading = false
                    |})

            let columns = [|
                {| field = "id"; headerName = "id"; width = 0; filterable = false; sortable = false |} |> box
                {| field = "group"; headerName = "Group"; width = 150; filterable = false; sortable = false |} |> box
                {| field = "medication"; headerName = "Medicatie"; width = 200; filterable = true; sortable = true |} |> box
                {| field = "quantity"; headerName = "Hoeveelheid"; width = 150; filterable = false; sortable = false |} |> box
                {| field = "rate"; headerName = "Stand"; width = 200; filterable = false; sortable = false |} |> box
                createObj [
                    "field" ==> "percentage"
                    "headerName" ==> "Aanpassen"
                    "width" ==> 350
                    "filterable" ==> false
                    "sortable" ==> false
                    "renderCell" ==> renderPercentageCell
                ]
            |]

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
                        height = "53vh"
                        onRowClick = ignore
                        checkboxSelection = true
                        selectedRows = [||]
                        onSelectChange = ignore
                        showToolbar = false
                        showFooter = false
                    |})
                }
            </Box>
            """


        JSX.jsx
            $"""
        import React from "react";
        import Stack from '@mui/material/Stack';
        import Box from '@mui/material/Box';
        import Button from '@mui/material/Button';
        import Accordion from '@mui/material/Accordion';
        import AccordionDetails from '@mui/material/AccordionDetails';
        import AccordionSummary from '@mui/material/AccordionSummary';
        import Typography from '@mui/material/Typography';
        import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
        import FormControlLabel from '@mui/material/FormControlLabel';

        <React.Fragment>
            <Accordion expanded={false} onChange={handleChange}>
                <AccordionSummary
                sx={ {| bgcolor=Mui.Colors.Grey.``100`` |} }
                expandIcon={{ <ExpandMoreIcon /> }}
                aria-controls="Enterale Voeding"
                id="nutrition-enteral"
                >
                Enterale Voeding
                </AccordionSummary>
                <AccordionDetails >
                    Enterale Voeding
                </AccordionDetails>
            </Accordion>
            <Accordion expanded={true} onChange={handleChange}>
                <AccordionSummary
                sx={ {| bgcolor=Mui.Colors.Grey.``100`` |} }
                expandIcon={{ <ExpandMoreIcon /> }}
                aria-controls="TPV en Vocht"
                id="nutrition-tpn"
                >
                Totale Parenterale Voeding
                </AccordionSummary>
                <AccordionDetails >
                    {tpn}
                </AccordionDetails>
            </Accordion>
        </React.Fragment>
        """
