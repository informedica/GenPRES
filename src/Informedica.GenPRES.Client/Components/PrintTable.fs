namespace Components


/// A printable list: a patient header at the top, the rows in a table with named columns, and a
/// signature line at the bottom. It prints the rows the screen shows, with the current filter and
/// search applied. The emphasis marks of the screen are left out.
module PrintTable =


    open Fable.Core
    open Feliz


    /// A printed column: the field it shows, its heading, and its width as a share of the page.
    type Column =
        {|
            field: string
            label: string
            width: string
        |}


    /// A cell of a row: the field of its column and its text.
    type Cell =
        {|
            field: string
            value: string
        |}


    /// A row: its cells, and the row actions of the list, which are not printed.
    type Row =
        {|
            cells: Cell[]
            actions: ReactElement option
        |}


    /// The columns, the rows, the patient header and the signature line.
    type Props =
        {|
            columns: Column[]
            rows: Row[]
            header: JSX.Element
            signature: JSX.Element
        |}


    let private tableSx =
        {|
            tableLayout = "fixed"
            width = "100%"
        |}


    // the columns have a fixed width, so a word longer than its column breaks within the column instead
    // of running into the next one; a narrow padding leaves the text of a column more room
    let private bodyCellSx =
        {|
            overflowWrap = "anywhere"
            paddingX = 1
        |}


    /// Returns the text of a row in a column with the asterisks of the emphasis removed, or an
    /// empty string when the row has no cell for the column.
    let valueOf (row: Row) field =
        row.cells
        |> Array.tryFind (fun cell -> cell.field = field)
        |> Option.map _.value
        |> Option.defaultValue ""
        |> _.Replace("*", "")


    [<JSX.Component>]
    let View (props: Props) =
        let headCells =
            props.columns
            |> Array.map (fun column ->
                let sx =
                    {|
                        fontWeight = "bold"
                        width = column.width
                        paddingX = 1
                    |}

                JSX.jsx
                    $"""
                import TableCell from '@mui/material/TableCell';

                <TableCell key={column.field} sx={sx}>{column.label}</TableCell>
                """
            )
            |> unbox<seq<ReactElement>>
            |> React.Fragment

        let bodyRows =
            props.rows
            |> Array.map (fun row ->
                let cells =
                    props.columns
                    |> Array.map (fun column ->
                        let value = valueOf row column.field

                        JSX.jsx
                            $"""
                        import TableCell from '@mui/material/TableCell';

                        <TableCell key={column.field} sx={bodyCellSx}>{value}</TableCell>
                        """
                    )
                    |> unbox<seq<ReactElement>>
                    |> React.Fragment

                JSX.jsx
                    $"""
                import TableRow from '@mui/material/TableRow';

                <TableRow key={valueOf row "id"}>
                    {cells}
                </TableRow>
                """
            )
            |> unbox<seq<ReactElement>>
            |> React.Fragment

        JSX.jsx
            $"""
        import React from 'react';
        import Table from '@mui/material/Table';
        import TableBody from '@mui/material/TableBody';
        import TableHead from '@mui/material/TableHead';
        import TableRow from '@mui/material/TableRow';

        <React.Fragment>
            {props.header}
            <Table size="small" sx={tableSx}>
                <TableHead>
                    <TableRow>
                        {headCells}
                    </TableRow>
                </TableHead>
                <TableBody>
                    {bodyRows}
                </TableBody>
            </Table>
            {props.signature}
        </React.Fragment>
        """
