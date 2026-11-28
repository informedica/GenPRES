namespace Components


module ResponsiveTable =

    open System
    open Informedica.GenPRES.Shared
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
                    // the row shown as marked; none when no row is marked
                    markedRow: string option
                    // each card has a checkbox before its title when true
                    checkboxSelection: bool
                    selectedRows: string[]
                    onSelectChange: string[] -> unit
                |})
            =

            // A column may carry a renderCard function for the cards. It is called like renderCell
            // of the grid, with the row id and the value, and the card cell shows its result.
            let renderCardOf (field: string) : (obj -> obj) option =
                props.columns
                |> Array.tryFind (fun c -> c.field = field)
                |> Option.bind (fun c ->
                    let render: obj = c?renderCard
                    if isNull render then None else Some(unbox render)
                )

            let cards =
                props.rows
                |> Array.map (fun row ->
                    let rowId =
                        row.cells
                        |> Array.tryFind (fun c -> c.field = "id")
                        |> Option.map _.value
                        |> Option.defaultValue ""

                    let handleClick = fun _ -> props.onRowClick rowId

                    let checkbox =
                        if props.checkboxSelection then
                            let isChecked = props.selectedRows |> Array.contains rowId

                            // a tap on the checkbox checks or unchecks the card and does not open it
                            let handleCheckClick (e: Browser.Types.Event) = e.stopPropagation ()

                            let handleCheckChange _ =
                                if isChecked then
                                    props.selectedRows |> Array.filter ((<>) rowId)
                                else
                                    Array.append props.selectedRows [| rowId |]
                                |> props.onSelectChange

                            let checkboxSx =
                                {|
                                    padding = 0
                                    marginRight = 1
                                |}

                            // the shown values of the card name the checkbox for a screen reader, because the
                            // title alone can be the same on several cards
                            let checkboxSlotProps =
                                let label =
                                    row.cells
                                    |> Array.filter (fun c ->
                                        c.field <> "id" && c.value |> String.isNullOrWhiteSpace |> not
                                    )
                                    |> Array.map _.value.Replace("*", "")
                                    |> String.concat " "

                                {| input = {| ``aria-label`` = label |} |}

                            JSX.jsx
                                $"""
                            import Checkbox from '@mui/material/Checkbox';

                            <Checkbox size="small" checked={isChecked} onClick={handleCheckClick} onChange={handleCheckChange} sx={checkboxSx} slotProps={checkboxSlotProps} />
                            """
                            |> toReact
                        else
                            null

                    // the marked card shows the light blue and the bar of a marked row in the grid
                    let cardSx =
                        if props.markedRow = Some rowId then
                            {|
                                cursor = "pointer"
                                backgroundColor = Mui.Colors.Blue.``50``
                                borderLeft = $"4px solid {Mui.Colors.Blue.``700``}"
                            |}
                            |> box
                        else
                            {| cursor = "pointer" |} |> box

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

                            let shown =
                                match renderCardOf cell.field with
                                | Some render -> render (createObj [ "id" ==> rowId; "value" ==> s ])
                                | None -> box s

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
                                import Stack from '@mui/material/Stack';

                                <Box sx={headerBoxSx} >
                                    <Stack direction="row" alignItems="center" >
                                        {checkbox}
                                        <Typography variant="subtitle2" component="div" sx={headerTypoSx} >
                                            {shown}
                                        </Typography>
                                    </Stack>
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
                                    <Typography variant="body2" component="div" sx={valueSx} >
                                        {shown}
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
                        <Card raised={true} onClick={handleClick} sx={cardSx} >
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


    /// The toolbar of the grid: an arrow that shows and hides the columns, density, export and print
    /// buttons. The buttons start hidden. The print button shows only when a print handler is given.
    /// The toolbar is a component of its own, so that the grid keeps it, and the focus on the arrow,
    /// when the buttons are shown or hidden.
    [<JSX.Component>]
    let Toolbar (props: {| onPrint: (unit -> unit) option |}) =
        let toolsOpen, setToolsOpen = React.useState false

        let toggleTools = fun _ -> setToolsOpen (not toolsOpen)

        // the arrow points right while the tools are hidden, and left to hide them again
        let toggleIcon =
            if toolsOpen then
                Mui.Icons.ChevronLeft
            else
                Mui.Icons.ChevronRight

        let toggleLabel =
            if toolsOpen then
                "Hide the table tools"
            else
                "Show the table tools"

        let printButton =
            match props.onPrint with
            | Some handlePrint ->
                JSX.jsx
                    $"""
                import Button from '@mui/material/Button';
                import PrintIcon from '@mui/icons-material/Print';

                <Button color="primary" size="small" startIcon={{<PrintIcon />}} onClick={fun _ -> handlePrint ()}>
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

        // no filter button and no filter in the column menus: the filter above the
        // grid is the one filter on the list, so that a row is never hidden by a
        // filter the user cannot see
        let tools =
            if toolsOpen then
                JSX.jsx
                    $"""
                import React from 'react';
                import {{ GridToolbarColumnsButton, GridToolbarDensitySelector, GridToolbarExport }} from '@mui/x-data-grid';

                <React.Fragment>
                    <GridToolbarColumnsButton />
                    <GridToolbarDensitySelector />
                    <GridToolbarExport printOptions={printOptionsSx} />
                    {printButton}
                </React.Fragment>
                """
            else
                null

        JSX.jsx
            $"""
        import {{ GridToolbarContainer }} from '@mui/x-data-grid';
        import IconButton from '@mui/material/IconButton';

        <GridToolbarContainer sx={toolbarSx}>
            <IconButton size="small" onClick={toggleTools} aria-label={toggleLabel} aria-expanded={toolsOpen} title={toggleLabel}>
                {toggleIcon}
            </IconButton>
            {tools}
        </GridToolbarContainer>
        """


    /// The slots of the grid with the toolbar. The object is made once, so that the grid gets the same
    /// toolbar component on every render and does not mount it anew.
    let toolbarSlots = createObj [ "toolbar" ==> Toolbar ]


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
                // the row shown as marked; none when no row is marked
                markedRow: string option
                checkboxSelection: bool
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

        let isMobile = Mui.Hooks.useIsMobile ()

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

        // Returns an alternating class name from the row index within the current page, with the
        // class marked added for the marked row.
        let getRowClassName =
            fun (pars: obj) ->
                let idx: int = pars?indexRelativeToCurrentPage
                let stripe = if idx % 2 = 0 then "even" else "odd"

                if props.markedRow = Some(pars?id |> string) then
                    $"%s{stripe} marked"
                else
                    stripe

        // Even rows get a grey background. The row under the pointer and the marked row show a blue
        // bar on the left, drawn as an inset shadow, because a border takes room and shifts the cells.
        // A click acts on the whole row, so the focus outline of the clicked cell is hidden; focus
        // from the keyboard keeps an outline.
        let rowBar = $"inset 4px 0 0 {Mui.Colors.Blue.``700``}"

        let stripedSx: obj =
            createObj
                [
                    "& .MuiDataGrid-row.even"
                    ==> createObj [ "backgroundColor" ==> Mui.Colors.Grey.``100`` ]

                    "& .MuiDataGrid-row"
                    ==> createObj [ "cursor" ==> "pointer"; "transition" ==> "box-shadow 0.1s ease" ]

                    "& .MuiDataGrid-row.even:hover"
                    ==> createObj [ "backgroundColor" ==> Mui.Colors.Grey.``100``; "boxShadow" ==> rowBar ]

                    "& .MuiDataGrid-row.odd:hover"
                    ==> createObj [ "backgroundColor" ==> "white"; "boxShadow" ==> rowBar ]

                    "& .MuiDataGrid-row.marked, & .MuiDataGrid-row.marked:hover"
                    ==> createObj [ "backgroundColor" ==> Mui.Colors.Blue.``50``; "boxShadow" ==> rowBar ]

                    "& .MuiDataGrid-cell:focus, & .MuiDataGrid-cell:focus-within"
                    ==> createObj [ "outline" ==> "none" ]

                    // a keyboard user still sees which cell has the focus, also when it is the checkbox inside it
                    "& .MuiDataGrid-cell:focus-visible, & .MuiDataGrid-cell:has(:focus-visible)"
                    ==> createObj
                            [
                                "outline" ==> $"2px solid {Mui.Colors.Blue.``700``}"
                                "outlineOffset" ==> "-2px"
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
            // only the cells the user sees: the grid hides the id column
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

        // the controls above the list, the same for the cards and the grid; the grid has its own
        // toolbar below them
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
                markedRow = props.markedRow
                checkboxSelection = props.checkboxSelection
                selectedRows = props.selectedRows
                onSelectChange = props.onSelectChange
            |}
            |> CardTable
        else
            let rows =
                rows
                |> Array.map _.cells
                |> Array.map (Array.map _.value)
                |> Array.map props.rowCreate

            let selectedRows =
                let jsIds: obj = emitJsExpr props.selectedRows "new Set($0)"

                {|
                    ``type`` = "include"
                    ids = jsIds
                |}

            let slots = if props.showToolbar then toolbarSlots else createObj []

            let slotProps =
                let onPrint =
                    props.onPrint
                    |> Option.map (fun handlePrint -> fun () -> handlePrint filteredRows)

                createObj [ "toolbar" ==> {| onPrint = onPrint |} ]

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
                        disableRowSelectionOnClick
                        disableColumnFilter
                        rowSelectionModel = {selectedRows}
                        onRowSelectionModelChange = {onSelectionChange}
                        getRowClassName={getRowClassName}
                        
                        rows={rows}
                        slots={slots}
                        slotProps={slotProps}
                        onRowClick={onRowClick}
                        hideFooter={not props.showFooter}
                        initialState =
                            { {| columns = {| columnVisibilityModel = {| id = false |} |} |} }
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
