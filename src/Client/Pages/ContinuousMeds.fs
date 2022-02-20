namespace Pages

module ContinuousMeds =

    open Feliz
    open Fable
    open Fable.React
    open Shared
    open Types
    open Components

    module TG = Utils.Typography

    let createHeadersAndRows w =
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
            w
            |> ContMeds.calcContMed
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



    [<ReactComponent>]
    let view (input: {| pat : Patient option |}) = 
        match input.pat |> Option.bind Patient.getWeight with
        | Some w ->
            let hs, rs = w |> createHeadersAndRows
            Html.div [
                prop.children  (
                    SortableTable.render hs rs ignore
                )
            ]
        | None ->
            Html.div [
                Utils.Typography.h3 "Voer leeftijd en/of gewicht in ..."
            ]

    let render pat = view ({| pat = pat |})