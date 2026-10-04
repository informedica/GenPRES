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
                        cells:
                            {|
                                sx: obj
                                content: obj
                            |}[]
                    |}[]
            |})
        =
        let rows =
            props.rows
            |> Array.map (fun row ->
                let cells =
                    row.cells
                    |> Array.mapi (fun i c ->
                        JSX.jsx
                            $"""
                        import TableCell from '@mui/material/TableCell';
                        <TableCell key={i} sx={c.sx}>
                            {c.content}
                        </TableCell>
                        """
                    )

                JSX.jsx
                    $"""
                import TableRow from '@mui/material/TableRow';
                <TableRow key={row.key}>
                    {cells}
                </TableRow>
                """
            )

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
