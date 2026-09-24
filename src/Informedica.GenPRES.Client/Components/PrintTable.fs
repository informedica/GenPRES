namespace Components


/// A list as it goes on paper: the patient it is for at the top, the rows in a table of named
/// columns, and a line to sign at the bottom. What is on paper is what the list showed, the
/// filter and the search of the moment included, so the sheet in the hand and the screen it was
/// printed from say the same thing. The emphasis the screen draws a value in has no meaning on
/// paper and is left out.
module PrintTable =


    open Fable.Core
    open Feliz


    /// One column on paper: which value of a row it shows, the name above it, and how wide it
    /// is, as a share of the page.
    type Column =
        {|
            field: string
            label: string
            width: string
        |}


    /// One cell of a row: the column it belongs to and what it says.
    type Cell =
        {|
            field: string
            value: string
        |}


    /// One row: its cells, and what the list offers to do with it, which paper has no use for.
    type Row =
        {|
            cells: Cell[]
            actions: ReactElement option
        |}


    /// The sheet: the columns, the rows, the patient it is for and the line to sign.
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


    /// The value of a row in a column, without the emphasis the screen drew it with.
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

                        <TableCell key={column.field}>{value}</TableCell>
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
