namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types



open Elmish
open Fable.Core.JsInterop


module BasicTable =


    [<JSX.Component>]
    let View (props: {| header: obj []; rows : obj [][] |}) =
        let createRow i cells =
            let key = $"{cells |> Array.head}-{i}"

            let cells =
                cells
                |> Array.mapi (fun i c ->
                    JSX.jsx $"""
                    import TableCell from '@mui/material/TableCell';
                    <TableCell key={i}>
                        {c}
                    </TableCell>
                    """
                )

            JSX.jsx $"""
            import TableRow from '@mui/material/TableRow';
            <TableRow key={key}>
                {cells}
            </TableRow>
            """

        let rows =
            props.rows
            |> Array.filter (Array.isEmpty >> not)
            |> Array.mapi createRow

        JSX.jsx
            $"""
        import Paper from '@mui/material/Paper';
        import Table from '@mui/material/Table';
        import TableBody from '@mui/material/TableBody';
        import TableContainer from '@mui/material/TableContainer';
        import TableHead from '@mui/material/TableHead';

        <Paper>
            <TableContainer >
                <Table>
                    <TableBody>
                        {rows}
                    </TableBody>
                </Table>
            </TableContainer>
        </Paper>
"""
