namespace Views

module Nutrion =


    open Fable.Core


    [<JSX.Component>]
    let View () =

        let handleChange = fun _ -> ()

        let tpn =
            let rows =
                [|
                    {| group = "eiwitten"; medication = "Samenstelling C"; quantity = "100 mL"; rate="" |}
                    {| group = "eiwitten"; medication = "glucose 10%"; quantity = "80 mL"; rate="" |}
                    {| group = "eiwitten"; medication = "NaCl 0,9%"; quantity = "10 mL"; rate="" |}
                    {| group = "eiwitten"; medication = "KCl 7,4%"; quantity = "10 mL"; rate="" |}
                    {| group = "eiwitten"; medication = "eiwitten totaal"; quantity = "200 mL"; rate="20 mL/uur" |}

                    {| group = "vetten"; medication = "intralipid 20%"; quantity = "10 mL"; rate="" |}
                    {| group = "vetten"; medication = "vitintra infant"; quantity = "5 mL"; rate="" |}
                    {| group = "vetten"; medication = "soluvit"; quantity = "5 mL"; rate="" |}
                    {| group = "vetten"; medication = "vetten totaal"; quantity = "20 mL"; rate="20 mL/uur" |}

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
                            |]
                        actions = None
                    |}
                )

            let rowCreate (cells : string []) =
                {|
                    id = cells[0]
                    group = cells[1].Replace("*", "")
                    medication = cells[2].Replace("*", "")
                    quantity = cells[3].Replace("*", "")
                    rate = cells[4].Replace("*", "")
                |}
                |> box

            let columns = [|
                {|  field = "id"; headerName = "id"; width = 0; filterable = false; sortable = false |}
                {|  field = "group"; headerName = "Group"; width = 150; filterable = false; sortable = false |}
                {|  field = "medication"; headerName = "Medicatie"; width = 200; filterable = true; sortable = true |}
                {|  field = "quantity"; headerName = "Hoeveelheid"; width = 150; filterable = false; sortable = false |}
                {|  field = "rate"; headerName = "Stand"; width = 200; filterable = false; sortable = false |}
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
                        height = "50vh"
                        onRowClick = ignore
                        checkboxSelection = false
                        selectedRows = [||]
                        onSelectChange = ignore
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
        import Grid from '@mui/material/Grid';
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
