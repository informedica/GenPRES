namespace Views

module Totals =


    open Fable.Core
    open Feliz
    open Shared
    open Types


    let private rows = Models.Totals.intakeRows


    let private splitIntoColumns maxCols (arr: 'a[]) =
        let n = arr.Length

        if n = 0 then
            [||]
        else
            let cols = min n maxCols
            let colSize = (n + cols - 1) / cols

            [|
                for c in 0 .. cols - 1 do
                    let start = c * colSize
                    let stop = min (start + colSize - 1) (n - 1)

                    if start < n then
                        arr[start..stop]
            |]


    let private typoGraphy (items: TextItem[]) =
        let variant = "body2"

        let normalSx =
            {|
                display = "inline"
                color = Mui.Colors.Grey.``700``
            |}

        let boldSx =
            {|
                display = "inline"
                color = Mui.Colors.BlueGrey.``700``
            |}

        let boxSx = {| display = "inline" |}

        let print item =
            match item with
            | Normal s ->
                JSX.jsx
                    $"""
                <Typography variant={variant} sx={normalSx}>{s}</Typography>
                """
            | Bold s ->
                JSX.jsx
                    $"""
                <Typography
                variant={variant}
                sx={boldSx}
                >
                <strong> {s} </strong>
                </Typography>
                """
            | Italic s ->
                JSX.jsx
                    $"""
                <Typography
                variant={variant}
                sx={normalSx}
                >
                {s}
                </Typography>
                """

        JSX.jsx
            $"""
        import Typography from '@mui/material/Typography';
        import Box from '@mui/material/Box';

        <Box sx={boxSx}>
            {items |> Array.map print |> unbox<seq<ReactElement>> |> React.Fragment}
        </Box>
        """


    [<JSX.Component>]
    let View (props: {| intake: Totals |}) =
        let mapRow (intake: Totals) row =
            let print n itms =
                if itms |> Array.length < 2 then
                    [||]
                else
                    [|
                        [| Normal n |] |> typoGraphy
                        itms[0 .. (itms.Length - 2)] |> typoGraphy
                        [| itms |> Array.last |] |> typoGraphy
                    |]
                |> Array.map box

            row
            |> Array.map (fun cells ->
                let name = cells |> Array.head
                let items = Models.Totals.substanceToField intake name
                print name items
            )

        let activeRows =
            rows
            |> Array.filter (fun cells ->
                let name = cells |> Array.head
                let items = Models.Totals.substanceToField props.intake name
                items |> Array.length >= 2
            )

        let columns = splitIntoColumns 3 activeRows

        let content =
            columns
            |> Array.mapi (fun i col ->
                $"table{i + 1}", Components.BasicTable.View {| rows = mapRow props.intake col |} |> toReact
            )

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        if isMobile then
            Unchecked.defaultof<JSX.Element>
        else
            let sxBox =
                {|
                    paddingTop = 2
                    paddingBottom = 2
                |}

            let sxStack = {| justifyContent = "center" |}

            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            import Box from '@mui/material/Box';
            <Box sx={sxBox}>
                <Stack sx={sxStack} direction="row" spacing={3} >
                    {content |> withKey}
                </Stack>
            </Box>
            """
