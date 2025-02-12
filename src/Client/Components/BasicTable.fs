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
    let View (props: {| header: string []; rows : string [][] |}) =
        let createRow cells =
            let key = cells |> Array.head

            let cells =
                cells
                |> Array.map (fun c ->
                    JSX.jsx $"""
                    import TableCell from '@mui/material/TableCell';
                    <TableCell>
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

        let rows = props.rows |> Array.map createRow

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
