namespace Views

module EmergencyList =

    open System
    open Feliz
    open Feliz.UseElmish
    open Elmish
    open Shared
    open Global
    open Types
    open Components
    open MaterialUI.Typography


    module TG = Utils.Typography


    type Msg = RowClick of int * string list


    let createHeadersAndRows lang (bolusMed: Intervention list) =
        let getTerm = Localization.getTerm lang

        let headers =
            [
                (Localization.Terms.``Emergency List Indication``
                 |> getTerm,
                 true)
                (Localization.Terms.``Emergency List Intervention``
                 |> getTerm,
                 true)
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
                let d =
                    if row.SubstanceDose.IsNone then
                        TG.subtitle2 row.SubstanceDoseText
                    else
                        Html.div [
                            prop.style [ style.display.inlineFlex ]
                            prop.children [
                                TG.subtitle2
                                    $"{row.SubstanceDose} {row.SubstanceDoseUnit}"
                                if row.SubstanceDoseAdjust.IsSome then
                                    Html.div [
                                        prop.style [ style.paddingLeft 5 ]
                                        prop.children [
                                            MaterialTypography.create [
                                                MaterialTypography.variant "subtitle2"
                                                MaterialTypography.color "textSecondary"
                                                prop.text
                                                    $"({row.SubstanceDoseAdjust} {row.SubstanceDoseAdjustUnit})"
                                            ]
                                        ]
                                    ]
                            ]
                        ]

                let p =
                    if row.InterventionDoseText = "" then
                        Html.div []
                    else
                        Html.div [
                            prop.style [ style.display.inlineFlex ]
                            prop.children [
                                TG.subtitle2
                                    $"{row.InterventionDose} {row.InterventionDoseUnit}"
                                Html.div [
                                    prop.style [ style.paddingLeft 5 ]
                                    prop.children [
                                        TG.body2
                                            $"van {row.Quantity} {row.QuantityUnit}/ml"
                                    ]
                                ]
                            ]
                        ]

                [
                    (row.Indication, TG.caption row.Indication)
                    (row.Name, TG.subtitle2 row.Name)
                    (row.SubstanceDoseText, d) //TG.body2 row.SubstanceDoseText)
                    (row.InterventionDoseText, p)
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


    let init () = (), Cmd.none


    let update handleRowClick msg state =
        match msg with
        | RowClick (i, els) ->
            state, Cmd.ofSub (fun _ -> (i, els) |> handleRowClick)


    [<ReactComponent>]
    let View
        (input: {| bolusMed: Intervention list
                   handleRowClick: int * string list -> unit |})
        =
        let lang =
            React.useContext (Global.languageContext)

        let _, dispatch =
            React.useElmish (init, update input.handleRowClick, [||])

        let hs, rs =
            input.bolusMed |> createHeadersAndRows lang

        SortableTable.render hs rs (RowClick >> dispatch)


    let render bolusMed handleRowClick =
        View(
            {|
                bolusMed = bolusMed
                handleRowClick = handleRowClick
            |}
        )