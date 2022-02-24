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

    module TG = Utils.Typography


    type State = { Dialog: string list }


    type Msg =
        | RowClick of int * string list
        | CloseDialog


    let init () = { Dialog = [] }, Cmd.none


    let update (msg: Msg) state =
        match msg with
        | RowClick (i, xs) ->
            Utils.Logging.log "rowclick:" i
            { state with Dialog = xs }, Cmd.none
        | CloseDialog -> { state with Dialog = [] }, Cmd.none


    let createHeadersAndRows w contMeds =
        let headers =
            [
                ("Indicatie", true)
                ("Medicatie", true)
                ("Hoeveelheid", false)
                ("Oplossing", false)
                ("Dosering", false)
                ("Advies", false)
            ]
            |> List.map (fun (lbl, b) -> (lbl |> Utils.Typography.subtitle2, b))

        let rows =
            ContMeds.calcContMed2 w contMeds
            |> List.map (fun row ->
                match row with
                | ind :: med :: qty :: sol :: dose :: adv :: [] ->
                    [
                        (ind, TG.caption ind)
                        (med, TG.subtitle2 med)
                        (qty, TG.body2 qty)
                        (sol, TG.body2 sol)
                        (dose, TG.body2 dose)
                        (adv, Html.div [ prop.text adv ])
                    ]
                | _ -> []
                |> List.map (fun (s, l) -> s.ToLower(), l)
            )

        headers, rows



    let createDialog state dispatch =
        let content =
            state.Dialog
            |> function
                | [ ind; med; qty; sol; dose; adv ] ->
                    [ ind; med; qty; sol; dose; adv ]
                    |> List.zip [
                        "indicatie"
                        "medicatie"
                        "hoeveelheid"
                        "oplossing"
                        "dosering"
                        "advies"
                       ]
                    |> List.collect (fun (s1, s2) ->
                        if s2 = "" then
                            []
                        else
                            [
                                Mui.listItem [
                                    Mui.listItemText [
                                        listItemText.primary s1
                                        if s1 = "medicatie" || s1 = "dosering" then
                                            listItemText.secondary (
                                                $"**{s2}**"
                                                |> Components.Markdown.render
                                            )
                                        else
                                            listItemText.secondary s2
                                    ]
                                ]
                                Mui.divider []
                            ]
                    )
                    |> Mui.list
                | _ ->
                    [
                        "No valid content" |> Components.Markdown.render
                    ]
                    |> React.fragment

        Mui.dialog [
            dialog.open' (state.Dialog |> List.isEmpty |> not)
            dialog.children [
                Mui.dialogTitle "Details"
                Mui.dialogContent [
                    prop.style [ style.padding 40 ]
                    prop.children content
                ]
                Mui.dialogActions [
                    Mui.button [
                        prop.onClick (fun _ -> CloseDialog |> dispatch)
                        prop.text "OK"
                    ]
                ]
            ]
            dialog.onClose (fun _ -> CloseDialog |> dispatch)
        ]


    [<ReactComponent>]
    let view (input: {| pat: Patient option; contMeds : Continuous list |}) =
        let state, dispatch =
            React.useElmish (init, update, [| box input.pat |])

        match input.pat |> Option.bind Patient.getWeight with
        | Some w when input.contMeds |> List.length > 1 ->

            let hs, rs = createHeadersAndRows w input.contMeds

            Html.div [
                prop.children [
                    createDialog state dispatch
                    SortableTable.render hs rs (RowClick >> dispatch)
                ]
            ]
        | _ ->
            Html.div [
                Utils.Typography.h3 "Voer leeftijd en/of gewicht in ..."
            ]

    let render patient contMeds = view ({| pat = patient; contMeds = contMeds |})