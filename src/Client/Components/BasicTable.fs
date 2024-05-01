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
    let View (props :
        {|
            columns : {|  field : string; headerName : string; width : int; filterable : bool; sortable : bool |}[]
            rows : {| cells : {| field: string; value: string |} []; actions : ReactElement option |} []
        |}) =

        JSX.jsx
            $"""
        import Table from '@mui/material/Table';
        import TableBody from '@mui/material/TableBody';
        import TableCell from '@mui/material/TableCell';
        import TableContainer from '@mui/material/TableContainer';
        import TableHead from '@mui/material/TableHead';
        import TableRow from '@mui/material/TableRow';
"""
