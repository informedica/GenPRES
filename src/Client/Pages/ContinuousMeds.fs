namespace Pages

module ContinuousMeds =

    open Feliz
    open Feliz.UseElmish
    open Elmish
    open Feliz.MaterialUI
    open Shared
    open Global
    open Types
    open Views
    open Components
    open MaterialUI.Typography

    module TG = Utils.Typography


    type State = { Dialog: string }


    type Msg =
        | RowClick of int * string list
        | CloseDialog


    let init () = { Dialog = "" }, Cmd.none


    let update (msg: Msg) state =
        match msg with
        | RowClick (i, xs) ->
            Utils.Logging.log "rowclick:" (i, xs)
            { state with Dialog = xs[1] }, Cmd.none
        | CloseDialog -> { state with Dialog = "" }, Cmd.none


    let createHeadersAndRows lang contMeds =
        let getTerm = Localization.getTerm lang

        let headers =
            [
                (Localization.Terms.``Continuous Medication Indication``
                 |> getTerm,
                 true)
                (Localization.Terms.``Continuous Medication Medication``
                 |> getTerm,
                 true)
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
                let q = $"{row.Quantity} {row.QuantityUnit}"
                let s = $"{row.Total} ml {row.Solution}"

                [
                    (row.Indication, TG.caption row.Indication)
                    (row.Name, TG.subtitle2 row.Name)
                    (q, TG.body2 q)
                    (s, TG.body2 s)
                    (row.SubstanceDoseText, TG.body2 row.SubstanceDoseText)
                    (row.Text,
                     MaterialTypography.create [
                         MaterialTypography.variant "body2"
                         MaterialTypography.color "textSecondary"
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

        let state, dispatch =
            React.useElmish (init, update, [| box input.pat |])

        match input.pat |> Option.bind Patient.getWeight with
        | Some _ when input.contMeds |> List.length > 1 ->

            let hs, rs =
                createHeadersAndRows lang input.contMeds

            Html.div [
                prop.children [
                    createDialog
                        state.Dialog
                        input.contMeds
                        input.products
                        dispatch
                    SortableTable.render hs rs (RowClick >> dispatch)
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