namespace Components

module Filter =

    open Elmish
    open Feliz
    open MaterialUI5
    open FSharp.Core
    open Feliz.UseElmish
    open Components
    open Shared
    open Localization
    open Utils.Sort

    type Msg =
        | SetSort of Sort
        | SetIntervention of string
        | SetIndication of string
        | Filter
        | ResetFilter

    type Model = {
        InterventionList : Intervention list
        FilteredList : Intervention list
        Sort: Sort
        Indications: string list
        SelectedIndication : Option<string>
        Interventions: string list
        SelectedIntervention: Option<string>
    }

    let init(interventionList : Intervention list) =
        {
            InterventionList = interventionList
            FilteredList = interventionList
            Sort = Sort.IndicationAsc
            Interventions = interventionList |> List.map (fun x -> x.Name) |> List.distinct |> List.sort;
            Indications = interventionList |> List.map (fun x -> x.Indication) |> List.distinct |> List.sort;
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
        | Some  s -> state.InterventionList
                    |> List.filter(fun bolus -> bolus.Indication = s)
                    |> List.map(fun item -> item.Name)
                    |> List.distinct
        | None -> state.InterventionList
                    |> List.map(fun item -> item.Name)
                    |> List.distinct

    let update publish msg state =

        match msg with
        | SetSort s -> { state with Sort = s}, Cmd.ofMsg Filter
        | SetIndication s -> { state with SelectedIndication = Some(s); Interventions = (interventions state)}, Cmd.ofMsg Filter
        | SetIntervention s -> {state with SelectedIntervention = Some(s);}, Cmd.ofMsg Filter
        | Filter ->
            let data = doSortAndFilter(state)
            publish data
            {state with FilteredList = data}, Cmd.none
        | ResetFilter ->
            publish (state.InterventionList |> List.sortBy( fun x-> x.Indication))
            {
                state with FilteredList = state.InterventionList |> List.sortBy( fun x-> x.Indication);
                            SelectedIndication = Option.None;
                            SelectedIntervention = Option.None;
                            Sort = Sort.IndicationAsc}, Cmd.none

    [<ReactComponent>]
    let View
        publish
        (props : {|
                    indicationLabel : ReactElement
                    interventionLabel : ReactElement
                    interventionList: Intervention list
                    lang : Locales

        |}) =

            let state, dispatch =
                    React.useElmish (init props.interventionList, update publish,[||])

            let sortValue = (Some(sortableItems |> List.find (fun (_, sort) -> sort = state.Sort) |> fst))
            let handleSortSelect = (fun item -> sortableItems |> List.find (fun (key, value) ->  key = item ) |> snd |> SetSort |> dispatch)
            let handleIndicationSelect =  (fun item -> indications state.Indications |> List.find ( fun key -> key = item) |> SetIndication |> dispatch )
            let handleInterventionSelect = (fun item -> interventions(state) |> List.find ( fun key -> key = item) |> SetIntervention |> dispatch )

            Html.div[
                    Mui.formGroup[
                        formGroup.row true
                        prop.style[style.display.flex]
                        formGroup.children[
                            Select.render props.indicationLabel (indications state.Indications) state.SelectedIndication handleIndicationSelect
                            Select.render props.interventionLabel (interventions state) state.SelectedIntervention handleInterventionSelect
                            Select.render (Utils.Typography.body1 (Localization.Terms.``Sort By`` |> getTerm props.lang)) sortItems sortValue handleSortSelect
                            Mui.button[
                                prop.text (Localization.Terms.``Reset Filter`` |> getTerm props.lang)
                                button.variant.outlined
                                prop.onClick (fun _ -> ResetFilter |> dispatch)
                            ]
                        ]
                    ]
                ]


    let render publish
            (props : {|
                    indicationLabel : ReactElement
                    interventionLabel : ReactElement
                    interventionList: Intervention list
                    lang : Locales
        |}) =
            View publish props
