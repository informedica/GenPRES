namespace Views

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


module ContinuousMeds =


    [<JSX.Component>]
    let View (props : {| interventions: Deferred<Shared.Types.Intervention list> |}) =

        let columns = [|
            {|  field = "id"; headerName = "id"; width = 0; filterable = false; sortable = false |}
            {|  field = "indication"; headerName = "Indicatie"; width = 200; filterable = true; sortable = true |}
            {|  field = "medication"; headerName = "Medicatie"; width = 200; filterable = true; sortable = true |}
            {|  field = "quantity"; headerName = "Hoeveelheid"; width = 150; filterable = false; sortable = false |}
            {|  field = "solution"; headerName = "Oplossing"; width = 150; filterable = false; sortable = false |} //``type`` = "number"
            {|  field = "dose"; headerName = "Dosering"; width = 230; filterable = false; sortable = false |} //``type`` = "number"
            {|  field = "advice"; headerName = "Advies"; width = 190; filterable = false; sortable = false |}
        |]

        let rows =
            match props.interventions with
            | Resolved items ->
                items
                |> List.toArray
                |> Array.mapi (fun i m ->
                    {|
                        cells =
                            [|
                                {| field = "id"; value = $"{i + 1}" |}
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
                    columns = columns
                    rows = rows
                    rowCreate = rowCreate
                |})
            }
        </Box>
        """
