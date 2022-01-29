namespace Components

module SortableTable =

    open Fable.Import
    open Fable
    open Feliz.MaterialUI
    open Feliz
    open Elmish
    open Utils
    
    type Model =
        { HeaderRow : Header list
          Rows : (string * ReactElement) list list
          Dialog : ReactElement list }

    and Header =
        { Label : string
          IsActive : bool
          IsSortable : bool
          SortedDirection : string option }

    let createHeader lbl isSort =
        { Label = lbl
          IsActive = false
          IsSortable = isSort
          SortedDirection = None }

    type Msg =
        | Sort of string
        | Click of ReactElement list
        | OnClose

    let update model msg =
        let toLower = Utils.String.toLower
        match msg with
        | Click els -> { model with Dialog = els }

        | OnClose -> { model with Dialog = [] }

        | Sort lbl ->
            let hr, sorted =
                match model.HeaderRow
                      |> List.tryFindIndex (fun h -> h.Label = lbl) with
                | Some i ->
                    model.HeaderRow
                    |> List.mapi (fun i' h ->
                        if i = i' then
                            { 
                                h with 
                                    IsActive = true
                                    SortedDirection =
                                        match h.SortedDirection with
                                        | None -> "asc" |> Some
                                        | Some dir ->
                                            match dir with
                                            | _ when dir = "asc" -> "desc" |> Some
                                            | _ ->                  "asc"  |> Some 
                            }
                        else
                            { 
                                h with 
                                    IsActive = false
                                    SortedDirection = None 
                            }
                    ),
                    match model.HeaderRow.[i].SortedDirection with
                    | Some dir ->
                        match dir with
                        | _ when dir = "asc" ->
                            model.Rows
                            |> List.sortByDescending (fun r ->
                                r.[i]
                                |> fst
                                |> toLower
                            )
                        | _ ->
                            model.Rows
                            |> List.sortBy (fun r ->
                                r.[i]
                                |> fst
                                |> toLower
                            )
                    | _ ->
                        model.Rows
                        |> List.sortBy (fun r ->
                            r.[i]
                            |> fst
                            |> toLower
                        )
                | None -> model.HeaderRow, model.Rows
            { 
                model with 
                    HeaderRow = hr
                    Rows = sorted 
            }


    let createHead dispatch { HeaderRow = items } =
        let sticky =
            prop.style [ 
                style.backgroundColor Colors.grey.``100``
                
                if Browser.Dom.window.screen.availWidth > 1000. then
                        style.position.sticky
                        //CSSProp.Position "sticky"
                else style.custom ("position", "-webkit-sticky")
                
                style.top 0
                style.zIndex 10 
            ]

        Mui.tableHead [
            items
            |> List.map (fun i ->
                let props =
                    match i.IsSortable, i.SortedDirection with
                    | true, Some d ->
                        [
                            if d = "asc" then 
                                tableCell.sortDirection.asc 
                            else tableCell.sortDirection.desc

                            prop.text i.Label
                            prop.onClick (fun _ ->
                                i.Label
                                |> Sort
                                |> dispatch
                            )
                        ]
                    | _ -> 
                        [   
                            tableCell.sortDirection.false'
                            prop.text i.Label
                        ]
                Mui.tableSortLabel props
                |> fun lbl -> 
                        Mui.tableCell [ 
                            sticky
                            tableCell.children lbl 
                        ]
            )
            |> Mui.tableRow 
        ]


    let createTableBody dispatch { HeaderRow = header; Rows = rows } =
        rows
        |> List.mapi (fun i row ->
            row
            |> List.map (fun (_, el) ->
                Mui.tableCell [
                    prop.onClick (fun _ ->
                        List.zip header row
                        |> List.map (fun (h, r) ->
                            Html.div [
                                prop.style [ style.display.flex; style.flexDirection.row ]
                                prop.text $"{h.Label}: {r |> fst}"
                            ]
                        )
                        |> Click
                        |> dispatch
                    )
                    tableCell.children [el]
                ]
            )
            |> fun els -> 
                Mui.tableRow [
                    tableRow.hover true
                    tableRow.children els
                ]
        )
        |> Mui.tableBody 


    [<ReactComponent>]
    let View (input : {| model : Model; dispatch : Msg -> unit |}) =
        match input.model.Dialog with
        | [] ->
            let head = input.model |> createHead input.dispatch
            let body = input.model |> createTableBody input.dispatch
            let tableView = Mui.table [ head; body ]
            tableView
        | _ ->
            Mui.dialog [ 
                prop.style [
                    style.padding 30
                ]
                dialog.open' true
                dialog.onClose(fun _ _ -> OnClose |> input.dispatch) 
                dialog.children input.model.Dialog
            ]


    let render model dispatch = View({| model = model; dispatch = dispatch |})