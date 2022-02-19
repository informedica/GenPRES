namespace Components


module SortableTable =

    open System
    open Fable
    open Elmish
    open Feliz
    open Feliz.UseElmish
    open Fable.MaterialUI
    open Feliz.MaterialUI


    type State<'a when 'a :> IComparable> =
        {
            Headers: Header list
            Rows: Row<'a> list
        }

    and Header =
        {
            Id: int
            Label: ReactElement
            IsSortable: bool
            SortedDirection: SortDirection
        }

    and SortDirection =
        | Unsorted
        | Ascending
        | Descending

    and Row<'a> = { Id: int; Cells: Cell<'a> list }
    and Cell<'a> = { SortBy: 'a; Element: ReactElement }


    let createHeader id isSort lbl =
        {
            Id = id
            Label = lbl
            IsSortable = isSort
            SortedDirection = Unsorted
        }


    let createHeaders headers =
        headers
        |> List.mapi (fun i (lbl, isSort) -> createHeader i isSort lbl)


    let createRows data =
        data
        |> List.mapi (fun i xs ->
            {
                Id = i
                Cells =
                    xs
                    |> List.map (fun (s, e) -> { SortBy = s; Element = e })
            }
        )


    type Msg<'a> =
        | RowClick of int * 'a list
        | Sort of int


    let sortState i state =
        match state.Headers.[i].SortedDirection with
        | Unsorted -> Ascending
        | Ascending -> Descending
        | Descending -> Unsorted
        |> fun dir ->
            { state with
                Headers =
                    state.Headers
                    |> List.mapi (fun i' h ->
                        if i' = i then
                            { h with SortedDirection = dir }
                        else
                            { h with SortedDirection = Unsorted }
                    )
                Rows =
                    match dir with
                    | Unsorted -> state.Rows |> List.sortBy (fun r -> r.Id)
                    | Ascending ->
                        state.Rows
                        |> List.sortBy (fun r -> r.Cells.[i].SortBy)
                    | Descending ->
                        state.Rows
                        |> List.sortByDescending (fun r -> r.Cells.[i].SortBy)
            }


    let init headers rows =
        {
            Headers = headers |> createHeaders
            Rows = rows |> createRows
        },
        Cmd.none


    let update rowClick msg state =
        match msg with
        | RowClick (i, xs) -> state, Cmd.ofSub (fun _ -> (i, xs) |> rowClick)
        | Sort i -> state |> sortState i, Cmd.none


    let createHead dispatch model =
        Mui.tableHead [
            model.Headers
            |> List.map (fun h ->
                let props =
                    if h.IsSortable then
                        match h.SortedDirection with
                        | Unsorted -> [ tableSortLabel.active false ] //tableCell.sortDirection.false' ]
                        | Ascending ->
                            [
                                tableSortLabel.active true
                                tableSortLabel.direction.asc
                            ]
                        | Descending ->
                            [
                                tableSortLabel.active true
                                tableSortLabel.direction.desc
                            ]
                        |> List.append [
                            prop.onClick (fun _ -> h.Id |> Sort |> dispatch)
                           ]
                    else
                        [
                            tableSortLabel.active false
                            tableSortLabel.hideSortIcon true
                        ]
                    |> List.append [
                        tableCell.children [ h.Label ]
                       ]

                Mui.tableSortLabel props
                |> fun lbl ->
                    Mui.tableCell [
                        prop.style [
                            style.backgroundColor Colors.grey.``200``
                        ]
                        tableCell.variant.head
                        tableCell.children lbl
                    ]
            )
            |> Mui.tableRow
        ]


    let createTableBody dispatch { Rows = rows } =
        rows
        |> List.map (fun row ->
            row.Cells
            |> List.map (fun c ->
                Mui.tableCell [
                    prop.onClick (fun _ ->
                        let els =
                            row.Cells |> List.map (fun cell -> cell.SortBy)

                        (row.Id, els) |> RowClick |> dispatch
                    )
                    tableCell.children [ c.Element ]
                ]
            )
            |> fun els ->
                Mui.tableRow [
                    prop.onClick (fun e -> Utils.Logging.log "rowclick" e)
                    tableRow.hover true
                    tableRow.children els
                ]
        )
        |> Mui.tableBody


    [<ReactComponent>]
    let View
        (input: {| headers: (ReactElement * bool) list
                   rows: ('a * ReactElement) list list
                   rowClick: int * 'a list -> unit |})
        =
        let state, dispatch =
            React.useElmish (
                init input.headers input.rows,
                update input.rowClick,
                [| box input.rows |]
            )

        let head = state |> createHead dispatch
        let body = state |> createTableBody dispatch

        Mui.table [
            table.stickyHeader true
            table.children [ head; body ]
        ]


    let render headers rows rowClick =
        View(
            {|
                headers = headers
                rows = rows
                rowClick = rowClick
            |}
        )