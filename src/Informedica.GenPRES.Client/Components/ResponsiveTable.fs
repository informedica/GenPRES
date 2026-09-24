namespace Components


module ResponsiveTable =

    open System
    open Shared
    open Fable.Core
    open Feliz
    open Fable.Core.JsInterop


    module private Cards =


        [<JSX.Component>]
        let CardTable
            (props:
                {|
                    columns:
                        {|
                            field: string
                            headerName: string
                            width: int
                            filterable: bool
                            sortable: bool
                        |}[]
                    rows:
                        {|
                            cells:
                                {|
                                    field: string
                                    value: string
                                |}[]
                            actions: ReactElement option
                        |}[]
                    filter: ReactElement option
                    onRowClick: string -> unit
                |})
            =

            let cards =
                props.rows
                |> Array.map (fun row ->
                    let rowId =
                        row.cells
                        |> Array.tryFind (fun c -> c.field = "id")
                        |> Option.map _.value
                        |> Option.defaultValue ""

                    let handleClick = fun _ -> props.onRowClick rowId

                    let content =
                        row.cells
                        |> Array.choose (fun cell ->
                            if cell.field = "id" || cell.value |> String.isNullOrWhiteSpace then
                                None
                            else
                                Some cell
                        )
                        |> Array.mapi (fun i cell ->
                            let b, s =
                                match cell.value with
                                | _ when cell.value.Contains("**") ->
                                    Mui.Colors.Blue.``900``, cell.value.Replace("**", "")
                                | _ when cell.value.Contains("*") ->
                                    Mui.Colors.Blue.``900``, cell.value.Replace("*", "")
                                | _ -> Mui.Colors.Grey.``700``, cell.value

                            if i = 0 then
                                let headerBoxSx =
                                    {|
                                        paddingY = 0.5
                                        backgroundColor = Mui.Styles.headerBgColor
                                        marginX = -1.5
                                        paddingX = 1.5
                                    |}

                                let headerTypoSx =
                                    {|
                                        fontWeight = 600
                                        lineHeight = 1.4
                                        color = b
                                    |}

                                JSX.jsx
                                    $"""
                                import Typography from '@mui/material/Typography';
                                import Box from '@mui/material/Box';

                                <Box sx={headerBoxSx} >
                                    <Typography variant="subtitle2" sx={headerTypoSx} >
                                        {s}
                                    </Typography>
                                </Box>
                                """
                                |> toReact
                            else
                                let h =
                                    props.columns
                                    |> Array.tryFind (fun c -> c.field = cell.field)
                                    |> function
                                        | Some h -> $"{h.headerName.ToLower()}: "
                                        | None -> $"{cell.field}: "

                                let stackSx = {| paddingY = 0.5 |}

                                let labelSx =
                                    {|
                                        minWidth = 80
                                        lineHeight = 1.4
                                        color = Mui.Colors.Grey.``900``
                                    |}

                                let valueSx =
                                    {|
                                        lineHeight = 1.4
                                        color = b
                                    |}

                                JSX.jsx
                                    $"""
                                import Stack from '@mui/material/Stack';
                                import Typography from '@mui/material/Typography';

                                <Stack direction="row" spacing={1} sx={stackSx} >
                                    <Typography variant="body2" sx={labelSx} >
                                        {h}
                                    </Typography>
                                    <Typography variant="body2" sx={valueSx} >
                                        {s}
                                    </Typography>
                                </Stack>
                                """
                                |> toReact
                        )

                    let hasActions = row.actions |> Option.isSome

                    let actions =
                        match row.actions with
                        | Some act ->
                            let actionsSx =
                                {|
                                    paddingTop = 0
                                    paddingBottom = 0.5
                                    paddingX = 1
                                |}

                            JSX.jsx
                                $"""
                            import CardActions from '@mui/material/CardActions';
                            <CardActions sx={actionsSx} >
                                {act}
                            </CardActions>
                            """
                            |> toReact
                        | None -> null

                    let divider =
                        let dividerSx = {| borderColor = Mui.Colors.Grey.``300`` |}

                        JSX.jsx
                            $"""
                        import Divider from '@mui/material/Divider';

                        <Divider sx={dividerSx} />
                        """
                        |> toReact

                    let bottomPad = if hasActions then 0.5 else 1

                    let contentSx =
                        {|
                            paddingTop = 1
                            paddingBottom = bottomPad
                            paddingX = 1.5
                            ``&:last-child`` = {| paddingBottom = bottomPad |}
                        |}

                    let gridItemSx =
                        {|
                            width = "100%"
                            mb = 0.5
                        |}

                    JSX.jsx
                        $"""
                    import Card from '@mui/material/Card';
                    import CardContent from '@mui/material/CardContent';
                    import Stack from '@mui/material/Stack';

                    <Grid item sx={gridItemSx} >
                        <Card raised={true} onClick={handleClick} sx={ {| cursor = "pointer" |} } >
                            <CardContent sx={contentSx} >
                                <Stack spacing={0} divider={divider} >
                                    {content}
                                </Stack>
                            </CardContent>
                            {actions}
                        </Card>
                    </Grid>
                    """
                )

            let columnSpacing =
                {|
                    xs = 1
                    sm = 2
                    md = 3
                |}

            JSX.jsx
                $"""
            import Grid from '@mui/material/Grid';
            import Stack from '@mui/material/Stack';

            <Stack id="responsive-card-table" >
                {props.filter |> Option.defaultValue null}
                <Grid container rowSpacing={1} columnSpacing={columnSpacing} >
                    {React.Fragment(cards |> unbox<seq<ReactElement>>)}
                </Grid>
            </Stack>
            """


    open Cards


    [<JSX.Component>]
    let View
        (props:
            {|
                hideFilter: bool
                columns: obj[]
                rows:
                    {|
                        cells:
                            {|
                                field: string
                                value: string
                            |}[]
                        actions: ReactElement option
                    |}[]
                rowCreate: string[] -> obj
                height: string
                onRowClick: string -> unit
                checkboxSelection: bool
                // the checkboxes greyed while the owner is busy, so no change is dropped
                selectDisabled: bool
                selectedRows: string[]
                onSelectChange: string[] -> unit
                showToolbar: bool
                showFooter: bool
                onPrint:
                    ({|
                            cells:
                                {|
                                    field: string
                                    value: string
                                |}[]
                            actions: ReactElement option
                        |}[]
                            -> unit) option
                // These two props must always be paired: both Some or both None.
                // Mixing them (one Some, one None) will cause filter changes to be silently discarded.
                selectedFilter: string[] option
                onFilterChange: (string[] -> unit) option
                // the label of the column filter above the grid
                filterLabel: string
                // the label of the text search above the grid; empty for no search
                searchLabel: string
            |})
        =
        let localState, setLocalState = React.useState [||]

        let state, setState =
            match props.selectedFilter, props.onFilterChange with
            | Some f, Some cb -> f, cb
            | _ -> localState, setLocalState

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        // the text typed into the search, matched against every cell of a row
        let query, setQuery = React.useState ""

        let search =
            if props.searchLabel |> String.length = 0 || props.rows |> Array.isEmpty then
                None
            else
                SearchField.View
                    {|
                        label = props.searchLabel
                        value = query
                        onChange = setQuery
                        disabled = false
                    |}
                |> Some

        let columnFilter =
            if props.hideFilter then
                None
            else if props.rows |> Array.isEmpty then
                None
            else
                props.columns
                |> Array.choose (fun c ->
                    let col =
                        unbox<
                            {|
                                field: string
                                headerName: string
                                width: int
                                filterable: bool
                                sortable: bool
                            |}
                         >
                            c

                    if col.filterable then Some col else None
                )
                |> Array.tryHead

        let filter =
            columnFilter
            |> Option.map (fun column ->
                let data =
                    props.rows
                    |> Array.map _.cells
                    |> Array.map (Array.filter (fun cell -> cell.field = column.field))
                    |> Array.collect (Array.map _.value)
                    |> Array.distinct
                    |> Array.sortBy _.ToLower()

                MultiPickField.View
                    {|
                        label = props.filterLabel
                        options = data |> Array.map (fun s -> s, s)
                        selected = state
                        onChange = setState
                        isLoading = false
                        enabled = true
                    |}
            )

        let onRowClick = fun pars -> pars?id |> string |> props.onRowClick
        let isRowSelectable = fun _ -> not props.selectDisabled

        // Return an alternating class name based on the row index within the current page
        let getRowClassName =
            fun (pars: obj) ->
                let idx: int = pars?indexRelativeToCurrentPage
                if idx % 2 = 0 then "even" else "odd"

        // Style for striped rows: apply background to even rows
        // Use lef border color blue to indicate selection
        let stripedSx: obj =
            createObj
                [
                    "& .MuiDataGrid-row.even"
                    ==> createObj [ "backgroundColor" ==> Mui.Colors.Grey.``100`` ]

                    "& .MuiDataGrid-row"
                    ==> createObj [ "cursor" ==> "pointer"; "transition" ==> "border-left 0.1s ease" ]

                    "& .MuiDataGrid-row.even:hover"
                    ==> createObj
                            [
                                "backgroundColor" ==> Mui.Colors.Grey.``100``
                                "borderLeft" ==> $"4px solid {Mui.Colors.Blue.``700``}"
                            ]

                    "& .MuiDataGrid-row.odd:hover"
                    ==> createObj
                            [
                                "backgroundColor" ==> "white"
                                "borderLeft" ==> $"4px solid {Mui.Colors.Blue.``700``}"
                            ]

                    "& .MuiDataGrid-cell"
                    ==> createObj
                            [
                                "whiteSpace" ==> "normal"
                                "wordWrap" ==> "break-word"
                                "lineHeight" ==> "1.5"
                                "paddingTop" ==> "8px"
                                "paddingBottom" ==> "8px"
                                "display" ==> "flex"
                                "alignItems" ==> "center"
                            ]
                ]

        let needle = query.Trim().ToLowerInvariant()

        let matchesQuery
            (r:
                {|
                    cells:
                        {|
                            field: string
                            value: string
                        |}[]
                    actions: ReactElement option
                |})
            =
            // the cells the user sees: the id is a hidden column, by the same name the grid hides it
            needle |> String.length = 0
            || r.cells
               |> Array.exists (fun cell -> cell.field <> "id" && cell.value.ToLowerInvariant().Contains needle)

        let rows =
            props.rows
            |> Array.filter (fun r ->
                match columnFilter with
                | None -> true
                | Some column ->
                    r.cells
                    |> Array.exists (fun cell ->
                        cell.field = column.field
                        && (state |> Array.isEmpty || state |> Array.exists ((=) cell.value))
                    )
            )
            |> Array.filter matchesQuery

        let filteredRows = rows

        // the row of the user's controls above the list, the same on the cards and on the grid;
        // not named toolbar, which the grid's own toolbar below is
        let controls =
            ListToolbar.View
                {|
                    search = search
                    filter = filter
                |}

        let onSelectionChange =
            fun selectionModel ->
                let selType: string = selectionModel?``type``
                let ids: string[] = emitJsExpr selectionModel?ids "Array.from($0)"

                let selectedIds =
                    if selType = "exclude" then
                        let excludeSet = ids |> Set.ofArray

                        rows
                        |> Array.choose (fun r ->
                            r.cells |> Array.tryFind (fun c -> c.field = "id") |> Option.map _.value
                        )
                        |> Array.filter (fun id -> excludeSet |> Set.contains id |> not)
                    else
                        ids

                props.onSelectChange selectedIds

        if isMobile then
            let typedColumns =
                props.columns
                |> Array.map
                    unbox<
                        {|
                            field: string
                            headerName: string
                            width: int
                            filterable: bool
                            sortable: bool
                        |}
                     >

            {|
                columns = typedColumns
                rows = rows
                filter = Some(controls |> toReact)
                onRowClick = props.onRowClick
            |}
            |> CardTable
        else
            let rows =
                rows
                |> Array.map _.cells
                |> Array.map (Array.map _.value)
                |> Array.map props.rowCreate

            let toolbar () =
                if props.showToolbar then
                    let printButton =
                        match props.onPrint with
                        | Some handlePrint ->
                            JSX.jsx
                                $"""
                            import Button from '@mui/material/Button';
                            import PrintIcon from '@mui/icons-material/Print';

                            <Button color="primary" size="small" startIcon={{<PrintIcon />}} onClick={fun _ -> handlePrint filteredRows}>
                                Print
                            </Button>
                            """
                        | None -> null

                    let toolbarSx = {| justifyContent = "flex-start" |}

                    let printOptionsSx =
                        {|
                            hideFooter = true
                            hideToolbar = true
                        |}

                    JSX.jsx
                        $"""
                    import {{ GridToolbarContainer, GridToolbarColumnsButton, GridToolbarDensitySelector, GridToolbarExport }} from '@mui/x-data-grid';

                    <GridToolbarContainer sx={toolbarSx}>
                        <GridToolbarColumnsButton />
                        <GridToolbarDensitySelector />
                        <GridToolbarExport printOptions={printOptionsSx} />
                        {printButton}
                    </GridToolbarContainer>
                    """
                    |> toReact
                else
                    null

            let selectedRows =
                let jsIds: obj = emitJsExpr props.selectedRows "new Set($0)"

                {|
                    ``type`` = "include"
                    ids = jsIds
                |}

            let slots =
                if props.showToolbar then
                    createObj [ "toolbar" ==> toolbar ]
                else
                    createObj []

            let containerSx =
                {|
                    height = props.height
                    display = "flex"
                    flexDirection = "column"
                |}

            let gridWrapperStyle =
                {|
                    flex = 1
                    minHeight = 0
                    width = "100%"
                |}

            JSX.jsx
                $"""
            import {{ DataGrid }} from '@mui/x-data-grid';

            <Box sx={containerSx}>
                {controls}
                <div style={gridWrapperStyle}>
                    <DataGrid
                        sx={stripedSx}
                        showToolbar={props.showToolbar}
                        checkboxSelection={props.checkboxSelection}
                        isRowSelectable={isRowSelectable}
                        disableRowSelectionOnClick
                        rowSelectionModel = {selectedRows}
                        onRowSelectionModelChange = {onSelectionChange}
                        getRowClassName={getRowClassName}
                        
                        rows={rows}
                        slots={slots}
                        onRowClick={onRowClick}
                        hideFooter={not props.showFooter}
                        initialState =
                            { {|
                                  columns = {| columnVisibilityModel = {| id = false |} |}
                                  density = "comfortable"
                              |} }
                        columns=
                            {props.columns
                             |> Array.map (fun c ->
                                 // Try to get the field property if it exists as a simple column
                                 try
                                     let simpleCol = unbox<{| field: string |}> c

                                     match simpleCol.field with
                                     | "id" -> createObj [ "field" ==> "id"; "hide" ==> true ]
                                     | _ -> c
                                 with _ ->
                                     c // Already an object with custom properties
                             )}
                    />
                    </div>
            </Box>
            """
