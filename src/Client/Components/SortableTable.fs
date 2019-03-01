namespace Components

module SortableTable =
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.Import
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.MaterialUI.Themes
    open Fable.PowerPack.Keyboard

    type Model =
        { HeaderRow : Header list
          Rows : (string * ReactElement) list list
          Dialog : ReactElement list }

    and Header =
        { Label : string
          IsActive : bool
          IsSortable : bool
          SortedDirection : TableSortDirection option }

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
        let toLower = Utils.Utils.String.toLower
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
                               { h with IsActive = true
                                        SortedDirection =
                                            match h.SortedDirection with
                                            | None ->
                                                TableSortDirection.Asc |> Some
                                            | Some dir ->
                                                match dir with
                                                | TableSortDirection.Asc ->
                                                    TableSortDirection.Desc
                                                    |> Some
                                                | TableSortDirection.Desc ->
                                                    TableSortDirection.Asc
                                                    |> Some }
                           else
                               { h with IsActive = false
                                        SortedDirection = None }),
                    match model.HeaderRow.[i].SortedDirection with
                    | Some dir ->
                        match dir with
                        | TableSortDirection.Asc ->
                            model.Rows
                            |> List.sortByDescending (fun r ->
                                   r.[i]
                                   |> fst
                                   |> toLower)
                        | TableSortDirection.Desc ->
                            model.Rows
                            |> List.sortBy (fun r ->
                                   r.[i]
                                   |> fst
                                   |> toLower)
                    | _ ->
                        model.Rows
                        |> List.sortBy (fun r ->
                               r.[i]
                               |> fst
                               |> toLower)
                | None -> model.HeaderRow, model.Rows
            { model with HeaderRow = hr
                         Rows = sorted }

    let createHead dispatch { HeaderRow = items } =
        let sticky =
            Style [ CSSProp.BackgroundColor Fable.MaterialUI.Colors.grey.``100``
                    (if Browser.screen.availWidth > 1000. then
                         CSSProp.Position "sticky"
                     else CSSProp.Position "-webkit-sticky")
                    CSSProp.Top "0"
                    CSSProp.ZIndex "10" ]
        tableHead []
            [ tableRow []
                  (items
                   |> List.map
                          (fun i ->
                          let props =
                              match i.IsSortable, i.SortedDirection with
                              | true, Some dir ->
                                  [ MaterialProp.Active i.IsActive :> IHTMLProp

                                    TableSortLabelProp.Direction dir :> IHTMLProp
                                    OnClick(fun _ ->
                                        i.Label
                                        |> Sort
                                        |> dispatch) :> IHTMLProp ]
                              | true, None ->
                                  [ MaterialProp.Active i.IsActive :> IHTMLProp
                                    OnClick(fun _ ->
                                        i.Label
                                        |> Sort
                                        |> dispatch) :> IHTMLProp ]
                              | _ -> []
                          tableCell [ sticky ]
                              [ tableSortLabel props [ str i.Label ] ])) ]

    let createTableBody dispatch { HeaderRow = header; Rows = rows } =
        tableBody []
            (rows
             |> List.mapi
                    (fun i row ->
                    tableRow [ TableRowProp.Hover true ]
                        (row
                         |> List.map (fun (_, el) ->
                                tableCell [ OnClick(fun _ ->
                                                let els =
                                                    List.zip header row
                                                    |> List.map
                                                           (fun (h, r) ->
                                                           div
                                                               [ Style
                                                                     [ CSSProp.Display
                                                                           "flex"

                                                                       CSSProp.Direction
                                                                           "row" ] ]
                                                               [ str
                                                                     (h.Label
                                                                      + ": ")
                                                                 str (r |> fst) ])
                                                els
                                                |> Click
                                                |> dispatch) ] [ el ]))))

    let private styles (theme : ITheme) : IStyles list = []

    let view' (classes : IClasses) (model : Model) dispatch =
        match model.Dialog with
        | [] ->
            let head = model |> createHead dispatch
            let body = model |> createTableBody dispatch
            let tableView = table [] [ head; body ]
            tableView
        | _ ->
            dialog [ MaterialProp.Open true
                     MaterialProp.OnClose(fun _ -> OnClose |> dispatch) ]
                [ div [ Style [ CSSProp.Padding "30px" ] ] model.Dialog ]

    // Boilerplate code
    // Workaround for using JSS with Elmish
    // https://github.com/mvsmal/fable-material-ui/issues/4#issuecomment-422781471
    type private IProps =
        abstract model : Model with get, set
        abstract dispatch : (Msg -> unit) with get, set
        inherit IClassesProps

    type private Component(p) =
        inherit PureStatelessComponent<IProps>(p)
        let viewFun (p : IProps) = view' p.classes p.model p.dispatch
        let viewWithStyles = withStyles (StyleType.Func styles) [] viewFun
        override this.render() =
            ReactElementType.create !!viewWithStyles this.props []

    let view model dispatch : ReactElement =
        let props =
            jsOptions<IProps> (fun p ->
                p.model <- model
                p.dispatch <- dispatch)
        ofType<Component, _, _> props []
