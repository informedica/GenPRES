namespace Views

module EmergencyList =

    open System
    open Feliz
    open Feliz.UseElmish
    open Elmish
    open Feliz.MaterialUI
    open Shared
    open Global
    open Types
    open Components


    module TG = Utils.Typography


    type Msg = RowClick of int * string list


    let joules =
        [
            1
            2
            3
            5
            7
            10
            20
            30
            50
            70
            100
            150
        ]


    let createHeadersAndRows age wght (bolusMed: Bolus list) =
        let headers =
            [
                ("Indicatie", true)
                ("Interventie", true)
                ("Berekend", false)
                ("Bereiding", false)
                ("Advies", false)
            ]
            |> List.map (fun (lbl, b) -> (lbl |> Utils.Typography.subtitle2, b))

        let rows =
            EmergencyTreatment.getTableData2 age wght bolusMed
            |> List.map (fun row ->
                match row with
                | ind :: interv :: calc :: prep :: adv :: [] ->
                    [
                        (ind, TG.caption ind)
                        (interv, TG.subtitle2 interv)
                        (calc, TG.body2 calc)
                        (prep, TG.body2 prep)
                        (adv, Html.div [ prop.text adv ])
                    ]
                | _ -> []
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
        (input: {| age: float option
                   weight: float option
                   bolusMed : Bolus list
                   handleRowClick: int * string list -> unit |})
        =
        let _, dispatch =
            React.useElmish (init, update input.handleRowClick, [||])

        let hs, rs =
            input.bolusMed
            |> createHeadersAndRows input.age input.weight

        SortableTable.render hs rs (RowClick >> dispatch)


    let render age weight bolusMed handleRowClick =
        View(
            {|
                age = age
                weight = weight
                bolusMed = bolusMed
                handleRowClick = handleRowClick
            |}
        )