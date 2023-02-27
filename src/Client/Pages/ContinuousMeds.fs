namespace Pages

module ContinuousMeds =

    open Feliz
    open Feliz.UseElmish
    open Elmish
    open MaterialUI5
    open Shared
    open Views
    open Components
    open FSharp.Core
    open Localization
    open Utils.Sort

    module TG = Utils.Typography


    type State = {
        Dialog: string
        ContMeds : Intervention list
        FilteredList : Intervention list
        }


    type Msg =
        | RowClick of int * string list
        | CloseDialog
        | Filter of Intervention list


    let init (contMeds) =
            { Dialog = "";
            ContMeds = contMeds;
            FilteredList = contMeds }, Cmd.none

    let update (msg: Msg) state =
        match msg with
        | RowClick (i, xs) ->
            Utils.Logging.log "rowclick:" (i, xs)
            { state with Dialog = xs[1] }, Cmd.none
        | CloseDialog -> { state with Dialog = "" }, Cmd.none
        | Filter filteredList-> {state with FilteredList = filteredList}, Cmd.none

    let quantity (intervention :Intervention) =
        $"{intervention.Quantity} {intervention.QuantityUnit}"
    let solution (intervention :Intervention) =
        $"{intervention.Total} {intervention.Solution}"

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
                                                    prop.text (Localization.Terms.``Continuous Medication Indication``|> getTerm)
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
                                                                prop.text (Localization.Terms.``Continuous Medication Medication``|> getTerm)
                                                            ]
                                                            Mui.typography[
                                                                prop.text med.Name
                                                            ]
                                                            Mui.typography[
                                                                typography.variant.subtitle2
                                                                prop.text (Localization.Terms.``Continuous Medication Quantity``|> getTerm)
                                                            ]
                                                            Mui.typography[
                                                                prop.text (quantity med)
                                                            ]
                                                            Mui.typography[
                                                                typography.variant.subtitle2
                                                                prop.text (Localization.Terms.``Continuous Medication Solution``|> getTerm)
                                                            ]
                                                            Mui.typography[
                                                                prop.text (solution med)
                                                            ]
                                                        ]
                                                    ]
                                                    Mui.grid[
                                                        grid.item
                                                        grid.xs 6
                                                        grid.children[
                                                            Mui.typography[
                                                                typography.variant.subtitle2
                                                                prop.text (Localization.Terms.``Continuous Medication Dose``|> getTerm)
                                                            ]
                                                            Mui.typography[
                                                                prop.text med.SubstanceDoseText
                                                            ]
                                                            Mui.typography[
                                                                typography.variant.subtitle2
                                                                prop.text (Localization.Terms.``Continuous Medication Advice``|> getTerm)
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


    let createHeadersAndRows lang contMeds =
        let getTerm = Localization.getTerm lang

        let headers =
            [
                (Localization.Terms.``Continuous Medication Indication``
                 |> getTerm,
                 false)
                (Localization.Terms.``Continuous Medication Medication``
                 |> getTerm,
                 false)
                (Localization.Terms.``Continuous Medication Quantity``
                 |> getTerm,
                 false)
                (Localization.Terms.``Continuous Medication Solution``
                 |> getTerm,
                 false)
                (Localization.Terms.``Continuous Medication Dose``
                 |> getTerm,
                 false)
                (Localization.Terms.``Continuous Medication Advice``
                 |> getTerm,
                 false)
            ]
            |> List.map (fun (lbl, b) -> (lbl |> Utils.Typography.subtitle2, b))

        let rows =
            contMeds
            |> List.map (fun row ->
                let q = quantity row
                let s = solution row

                [
                    (row.Indication, TG.caption row.Indication)
                    (row.Name, TG.subtitle2 row.Name)
                    (q, TG.body2 q)
                    (s, TG.body2 s)
                    (row.SubstanceDoseText, TG.body2 row.SubstanceDoseText)
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



    let createDialog
        (med: string)
        (meds: Intervention list)
        (prods: Product list)
        dispatch
        =
        let med =
            meds |> List.tryFind (fun m -> m.Name = med)

        let title, content =
            match med with
            | Some med ->
                let title =
                    let q =
                        med.Quantity |> Option.defaultValue 0.

                    $"Bereiding voor {med.Name} {q} {med.QuantityUnit} in {med.Total} ml {med.Solution}"

                let content =
                    let prod =
                        prods
                        |> List.find (fun p -> p.Medication = med.Name)

                    let q =
                        (med.Quantity |> Option.defaultValue 0.)
                        / prod.Concentration

                    let t =
                        med.Total
                        |> Option.map (fun x ->
                            if x - q <= 0. then 0. else x - q
                        )
                        |> Option.defaultValue 0.

                    $"""{q} ml van {prod.Medication} {prod.Concentration} {prod.Unit}/ml
                    + {t} ml {med.Solution}"""
                    |> Utils.Typography.h5

                title, content
            | None -> "", Html.none


        Mui.dialog [
            dialog.open' (med |> Option.isSome)
            dialog.maxWidth.xl
            dialog.children [
                Mui.dialogTitle title
                Mui.dialogContent [
                    prop.style [ style.padding 40 ]
                    prop.children content
                    dialogContent.dividers true
                ]
                Mui.dialogActions [
                    Mui.button [
                        button.variant.outlined
                        prop.onClick (fun _ -> CloseDialog |> dispatch)
                        prop.text "OK"
                    ]
                ]
            ]
            dialog.onClose (fun _ -> CloseDialog |> dispatch)
        ]


    [<ReactComponent>]
    let view
        (input: {| pat: Patient option
                   contMeds: Intervention list
                   products: Product list |})
        =
        let lang =
            React.useContext (Global.languageContext)

        let isMobile = Hooks.useMediaQuery "(max-width:750px)"

        let state, dispatch =
            React.useElmish (init input.contMeds, update, [| box input.pat |])

        match input.pat |> Option.bind Patient.getWeight with
        | Some _ when input.contMeds |> List.length > 1 ->

            let hs, rs =
                createHeadersAndRows lang state.FilteredList

            let publish filteredList = dispatch (Filter filteredList)

            Html.div[
                Filter.render publish{|
                    indicationLabel = (Utils.Typography.body1 (Localization.Terms.``Continuous Medication Indication`` |> getTerm lang))
                    interventionLabel = (Utils.Typography.body1 (Localization.Terms.``Continuous Medication Medication`` |> getTerm lang))
                    interventionList = input.contMeds
                    lang = lang
                |}

                if isMobile then
                    Html.div[
                        createCards lang state.FilteredList
                    ]
                else
                    Html.div [
                        prop.children [
                            createDialog
                                state.Dialog
                                state.ContMeds
                                input.products
                                dispatch
                            SortableTable.render hs rs (RowClick >> dispatch)
                        ]
                    ]
            ]
         | _ ->
             Html.div [
                 Localization.Terms.``Continuous Medication List show when patient data``
                 |> Localization.getTerm lang
                 |> Utils.Typography.h3
             ]

    let render patient contMeds prods =
        view (
            {|
                pat = patient
                contMeds = contMeds
                products = prods
            |}
        )