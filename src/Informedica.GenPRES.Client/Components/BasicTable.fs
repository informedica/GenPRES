namespace Components


module BasicTable =


    open Fable.Core


    [<JSX.Component>]
    let View
        (props:
            {|
                rows:
                    {|
                        key: string
                        sx: obj
                        cells: obj[]
                    |}[]
            |})
        =
        let createRow
            (row:
                {|
                    key: string
                    sx: obj
                    cells: obj[]
                |})
            =
            let cells =
                row.cells
                |> Array.mapi (fun i c ->
                    JSX.jsx
                        $"""
                    import TableCell from '@mui/material/TableCell';
                    <TableCell key={i}>
                        {c}
                    </TableCell>
                    """
                )

            JSX.jsx
                $"""
            import TableRow from '@mui/material/TableRow';
            <TableRow key={row.key} sx={row.sx}>
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
