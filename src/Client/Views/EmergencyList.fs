namespace Views

module EmergencyList =

    open Feliz
    open Feliz.UseElmish
    open Elmish
    open MaterialUI5
    open Shared
    open Components
    open Localization
    open Utils.Sort
    open System.Collections.Generic


    module TG = Utils.Typography

    type Msg =
        | RowClick of int * string list
        | SetSort of Sort
        | SetIntervention of string
        | SetIndication of string
        | Filter
        | ResetFilter

    type Model = {
        BolusMed : Intervention list
        FilteredList : Intervention list
        Sort: Sort
        Indications: string list
        SelectedIndication : Option<string>
        Interventions: string list
        SelectedIntervention: Option<string>
    }

    let dose (intervention :Intervention) =
        if intervention.SubstanceDose.IsNone then
                        TG.subtitle2 intervention.SubstanceDoseText
                    else
                        Html.div [
                            prop.style [ style.display.inlineFlex ]
                            prop.children [
                                TG.subtitle2
                                    $"{intervention.SubstanceDose} {intervention.SubstanceDoseUnit}"
                                if intervention.SubstanceDoseAdjust.IsSome then
                                    Html.div [
                                        prop.style [ style.paddingLeft 5 ]
                                        prop.children [
                                            Mui.typography [
                                                typography.variant.subtitle2
                                                //TODO Fix Color
                                                //typography.color.textSecondary
                                                prop.text
                                                    $"({intervention.SubstanceDoseAdjust} {intervention.SubstanceDoseAdjustUnit})"
                                            ]
                                        ]
                                    ]
                            ]
                        ]
    let interventionDose (intervention :Intervention) =
        if intervention.InterventionDoseText = "" then
                        Html.div [
                            TG.body2 "N/A"
                        ]
                    else
                        Html.div [
                            prop.style [ style.display.inlineFlex ]
                            prop.children [
                                TG.subtitle2
                                    $"{intervention.InterventionDose} {intervention.InterventionDoseUnit}"
                                Html.div [
                                    prop.style [ style.paddingLeft 5 ]
                                    prop.children [
                                        TG.body2
                                            $"van {intervention.Quantity} {intervention.QuantityUnit}/ml"
                                    ]
                                ]
                            ]
                        ]


    let createCards (lang : Locales) (contMeds : Intervention list) =

        let getTerm = Localization.getTerm lang

        let cards = contMeds
                    |> List.map (fun med ->
                                Mui.card[
                                    prop.style [ style.margin 10]
                                    card.variant.outlined
                                    card.raised true
                                    card.children[
                                        Mui.cardHeader[
                                            prop.style[
                                                    style.display.flex
                                                ]
                                            cardHeader.title [
                                                Mui.typography[
                                                    typography.variant.h6
                                                    prop.text (Localization.Terms.``Emergency List Indication``|> getTerm)
                                                ]
                                                Mui.typography[
                                                    typography.variant.subtitle1
                                                    prop.text med.Indication
                                                ]
                                            ]
                                        ]
                                        Mui.cardContent[
                                            Mui.grid[
                                                grid.container
                                                grid.spacing 2
                                                grid.direction.row
                                                grid.children[
                                                    Mui.grid [
                                                        grid.item
                                                        grid.xs 6
                                                        grid.children[
                                                            Mui.typography[
                                                                typography.variant.subtitle2
                                                                prop.text (Localization.Terms.``Emergency List Intervention``|> getTerm)
                                                            ]
                                                            Mui.typography[
                                                                prop.text med.Name
                                                            ]
                                                            Mui.typography[
                                                                typography.variant.subtitle2
                                                                prop.text (Localization.Terms.``Emergency List Calculated``|> getTerm)
                                                            ]
                                                            dose med
                                                        ]
                                                    ]
                                                    Mui.grid[
                                                        grid.item
                                                        grid.xs 6
                                                        grid.children[
                                                            Mui.typography[
                                                                typography.variant.subtitle2
                                                                prop.text (Localization.Terms.``Emergency List Preparation``|> getTerm)
                                                            ]
                                                            interventionDose med
                                                            Mui.typography[
                                                                typography.variant.subtitle2
                                                                prop.text (Localization.Terms.``Emergency List Advice``|> getTerm)
                                                            ]
                                                            Mui.typography[
                                                                prop.text med.Text
                                                            ]
                                                        ]
                                                    ]
                                                ]

                                            ]
                                        ]
                                        ]

                                ] )
        Html.div [
            for card in cards do
                card
        ]

    let createHeadersAndRows lang (bolusMed: Intervention list) =
        let getTerm = Localization.getTerm lang

        let headers =
            [
                (Localization.Terms.``Emergency List Indication``
                |> getTerm,
                false)
                (Localization.Terms.``Emergency List Intervention``
                |> getTerm,
                false)
                (Localization.Terms.``Emergency List Calculated``
                |> getTerm,
                false)
                (Localization.Terms.``Emergency List Preparation``
                |> getTerm,
                false)
                (Localization.Terms.``Emergency List Advice``
                |> getTerm,
                false)
            ]
            |> List.map (fun (lbl, b) -> (lbl |> Utils.Typography.subtitle2, b))

        let rows =
            bolusMed
            |> List.map (fun row ->
                let d = dose row

                let p = interventionDose row

                [
                    (row.Indication, TG.caption row.Indication)
                    (row.Name, TG.subtitle2 row.Name)
                    (row.SubstanceDoseText, d) //TG.body2 row.SubstanceDoseText)
                    (row.InterventionDoseText, p)
                    (row.Text,
                    Mui.typography [
                        typography.variant.body2
                         //TODO Fix Color
                         //typography.color.textSecondary
                        prop.text row.Text
                    ])
                ]
                |> List.map (fun (s, l) -> s.ToLower(), l)
            )

        headers, rows


    let init (bolusMed) = {
        BolusMed = bolusMed;
        FilteredList = bolusMed
        Sort = Sort.IndicationAsc;
        Interventions = bolusMed |> List.map (fun x -> x.Name) |> List.distinct |> List.sort;
        Indications = bolusMed |> List.map (fun x -> x.Indication) |> List.distinct |> List.sort;
        SelectedIndication = Option.None;
        SelectedIntervention = Option.None}, Cmd.ofMsg(SetSort IndicationAsc;)

    let doSortAndFilter(state : Model): Intervention list =

        Browser.Dom.console.table state
        let indicationList = match state.SelectedIndication with
                                | Some s -> state.FilteredList |> List.filter (fun bolus -> bolus.Indication = s)
                                | None -> state.FilteredList

        let interventionList = match state.SelectedIntervention with
                                | Some s -> indicationList |> List.filter (fun bolus -> bolus.Name = s)
                                | None -> indicationList

        let sortedList = match state.Sort with
                            | IndicationAsc -> interventionList |> List.sortBy (fun item -> item.Indication )
                            | IndicationDesc -> interventionList |> List.sortByDescending (fun item -> item.Indication )
                            | InterventionAsc -> interventionList |> List.sortBy (fun item -> item.Name )
                            | InterventionDesc -> interventionList |> List.sortByDescending (fun item -> item.Name )

        sortedList

    let indications indications =
       indications |> List.distinct |> List.sort

    let interventions (state: Model) =
        match state.SelectedIndication with
        | Some  s -> state.BolusMed
                    |> List.filter(fun bolus -> bolus.Indication = s)
                    |> List.map(fun item -> item.Name)
                    |> List.distinct
        | None -> state.BolusMed
                    |> List.map(fun item -> item.Name)
                    |> List.distinct

    let update handleRowClick msg state =

        Browser.Dom.console.table msg
        match msg with
        | RowClick (i, els) ->
            state, Cmd.ofSub (fun _ -> (i, els) |> handleRowClick)
        | SetSort _ ->  state, Cmd.ofMsg Filter
        | SetIndication s -> { state with SelectedIndication = Some(s); Interventions = interventions state}, Cmd.ofMsg Filter
        | SetIntervention s -> {state with SelectedIntervention = Some(s);}, Cmd.ofMsg Filter
        | Filter -> {state with FilteredList = doSortAndFilter(state)}, Cmd.none
        | ResetFilter -> {
                state with FilteredList = state.BolusMed |> List.sortBy( fun x-> x.Indication);
                            SelectedIndication = Option.None;
                            SelectedIntervention = Option.None}, Cmd.none


    [<ReactComponent>]
    let View
        (input: {| bolusMed: Intervention list
                   handleRowClick: int * string list -> unit |})
        =

        let lang =
            React.useContext (Global.languageContext)

        let isMobile = Hooks.useMediaQuery "(max-width:750px)"

        let state, dispatch =
            React.useElmish (init input.bolusMed, update input.handleRowClick,[||])

        let hs, rs =
            state.FilteredList |> createHeadersAndRows lang

        let sortValue = (Some(sortableItems |> List.find (fun (_, sort) -> sort = state.Sort) |> fst))
        let handleSortSelect = (fun item -> sortableItems |> List.find (fun (key, value) ->  key = item ) |> snd |> SetSort |> dispatch)
        let handleIndicationSelect =  (fun item -> indications state.Indications |> List.find ( fun key -> key = item) |> SetIndication |> dispatch )
        let handleInterventionSelect =  (fun item -> interventions state |> List.find ( fun key -> key = item) |> SetIntervention |> dispatch )

        Html.div[
            Html.div[
                Mui.formGroup[
                    formGroup.row true
                    prop.style[style.display.flex]
                    formGroup.children[
                        Select.render (Utils.Typography.body1 (Localization.Terms.``Emergency List Indication`` |> getTerm lang)) (indications state.Indications) state.SelectedIndication handleIndicationSelect
                        Select.render (Utils.Typography.body1 (Localization.Terms.``Emergency List Intervention`` |> getTerm lang)) (interventions state) state.SelectedIntervention handleInterventionSelect
                        Mui.button[
                            prop.text "Reset Filter"
                            button.variant.outlined
                            prop.onClick (fun _ -> ResetFilter |> dispatch)
                        ]
                        Select.render (Utils.Typography.body1 (Localization.Terms.``Sort By`` |> getTerm lang)) sortItems sortValue handleSortSelect
                    ]
                ]
            ]


            if isMobile then
                Html.div[
                    createCards lang state.FilteredList
                ]
            else
                SortableTable.render hs rs (RowClick >> dispatch)
        ]

    let render bolusMed handleRowClick =
        View(
            {|
                bolusMed = bolusMed
                handleRowClick = handleRowClick
            |}
        )