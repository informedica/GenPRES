namespace Views

module Totals =


    open Fable.Core
    open Fable.Core.JsInterop
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


    // a changed value lights up, holds its colour and then fades back to the table's own background
    let private flashMs = 3000


    let private flashSx =
        createObj
            [
                "animation" ==> $"totalsFlash %i{flashMs}ms ease-out"
                "@keyframes totalsFlash"
                ==> {|
                        ``0%`` = {| backgroundColor = Mui.Styles.changedBgColor |}
                        ``40%`` = {| backgroundColor = Mui.Styles.changedBgColor |}
                        ``100%`` = {| backgroundColor = "transparent" |}
                    |}
            ]


    let private noFlashSx = {| |}


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
    let View
        (props:
            {|
                source: string
                intake: Totals
            |})
        =
        let seenRef = React.useRef (TotalsChangePolicy.initial props.source props.intake)
        let timerRef = React.useRef (None: int option)

        let flash, setFlash =
            React.useState
                {|
                    names = Set.empty<string>
                    revision = 0
                |}

        let observeTotals () =
            let seen, changed = seenRef.current |> TotalsChangePolicy.observe props.source props.intake

            seenRef.current <- seen

            if changed |> Array.isEmpty |> not then
                let revision = flash.revision + 1
                timerRef.current |> Option.iter JS.clearTimeout

                setFlash
                    {|
                        names = Set.ofArray changed
                        revision = revision
                    |}

                timerRef.current <-
                    JS.setTimeout
                        (fun () ->
                            setFlash
                                {|
                                    names = Set.empty
                                    revision = revision
                                |}
                        )
                        flashMs
                    |> Some

        React.useEffect (observeTotals, [| box props.source; box props.intake |])

        React.useEffect ((fun () -> fun () -> timerRef.current |> Option.iter JS.clearTimeout), [||])

        let mapRow (intake: Totals) row =
            let cell sx content =
                {|
                    sx = box sx
                    content = box content
                |}

            row
            |> Array.map (fun cells ->
                let name = cells |> Array.head
                let items = Models.Totals.substanceToField intake name
                let isChanged = flash.names |> Set.contains name

                {|
                    // the revision in the key remounts a row that changes again while it fades,
                    // so the fade starts over
                    key = if isChanged then $"%s{name}-%i{flash.revision}" else name
                    cells =
                        [|
                            [| Normal name |] |> typoGraphy |> cell noFlashSx
                            items[0 .. (items.Length - 2)]
                            |> typoGraphy
                            |> cell (if isChanged then flashSx else box noFlashSx)
                            [| items |> Array.last |] |> typoGraphy |> cell noFlashSx
                        |]
                |}
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

            // each card grows to share the row, so the bar is as wide as the page content above
            // it; a long value wraps rather than pushing the row past the container
            let sxStack =
                {|
                    ``& > *`` =
                        {|
                            flex = 1
                            minWidth = 0
                        |}
                |}

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
