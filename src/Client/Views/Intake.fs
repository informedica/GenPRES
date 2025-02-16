namespace Views

module Intake =

    open System
    open Fable.Core
    open Fable.React
    open Feliz
    open Browser.Types
    open Shared
    open Types
    open Elmish
    open Fable.Core.JsInterop

    let private rows1 = [|
        [| "volume"; ""; "ml/kg/dag" |]
        [| "energie"; ""; "kCal/kg/dag" |]
        [| "koolhydraat"; ""; "mg/kg/min" |]
        [| "eiwit"; ""; "g/kg/dag" |]
        [| "vet"; ""; "g/kg/dag" |]

    |]

    let private rows2 = [|
        [| "natrium"; ""; "mmol/kg/dag" |]
        [| "kalium"; ""; "mmol/kg/dag" |]
        [| "chloride"; ""; "mmol/kg/dag" |]
        [| "caldium"; ""; "mmol/kg/dag" |]
        [| "magnesium"; ""; "mmol/kg/dag" |]
    |]

    let private rows3 = [|
        [| "fosfaat"; ""; "mmol/kg/dag" |]
        [| "ijzer"; ""; "mmol/kg/dag" |]
        [| "vit D"; ""; "mmol/kg/dag" |]
        [| "ethanol"; ""; "mg/kg/dag" |]
        [| "propyleenglycol"; ""; "mg/kg/dag" |]
    |]

    let private rows4 = [|
        [| "boorzuur"; ""; "mmol/kg/dag" |]
        [| "benzylalcohol"; ""; "mmol/kg/dag" |]
    |]


    let private typoGraphy (item : Types.TextItem) =
        let print item =
            match item with
            | Normal s ->
                JSX.jsx
                    $"""
                <Typography color={Mui.Colors.Grey.``700``} display="inline">{s}</Typography>
                """
            | Bold s ->
                JSX.jsx
                    $"""
                <Typography
                color={Mui.Colors.BlueGrey.``500``}
                display="inline"
                >
                <strong> {s} </strong>
                </Typography>
                """
            | Italic s ->
                JSX.jsx
                    $"""
                <Typography
                color={Mui.Colors.Grey.``700``}
                display="inline"
                variant="caption"
                >
                <strong> {s} </strong>
                </Typography>
                """
            |> Array.singleton

        JSX.jsx
            $"""
        import Typography from '@mui/material/Typography';
        import Box from '@mui/material/Box';

        <Box display="inline" >
            {item |> print |> unbox |> React.fragment}
        </Box>
        """


    [<JSX.Component>]
    let View(res: Deferred<Intake>) =
        let mapRow (intake: Intake) row =
            row
            |> Array.map (fun cells ->
                match cells |> Array.head with
                | "volume" ->
                    if intake.Volume |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Volume[0] |> typoGraphy
                            intake.Volume[1] |> typoGraphy
                        |]
                | "energie" ->
                    if intake.Energy |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Energy[0] |> typoGraphy
                            intake.Energy[1] |> typoGraphy
                        |]
                | "koolhydraat" ->
                    if intake.Carbohydrate |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Carbohydrate[0] |> typoGraphy
                            intake.Carbohydrate[1] |> typoGraphy
                        |]
                | "eiwit" ->
                    if intake.Protein |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Protein[0] |> typoGraphy
                            intake.Protein[1] |> typoGraphy
                        |]
                | "vet" ->
                    if intake.Fat |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Fat[0] |> typoGraphy
                            intake.Fat[1] |> typoGraphy
                        |]
                | "natrium" ->
                    if intake.Sodium |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Sodium[0] |> typoGraphy
                            intake.Sodium[1] |> typoGraphy
                        |]
                | "kalium" ->
                    if intake.Potassium |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Potassium[0] |> typoGraphy
                            intake.Potassium[1] |> typoGraphy
                        |]
                | "chloride" ->
                    if intake.Chloride |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Chloride[0] |> typoGraphy
                            intake.Chloride[1] |> typoGraphy
                        |]
                | "calcium" ->
                    if intake.Calcium |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Calcium[0] |> typoGraphy
                            intake.Calcium[1] |> typoGraphy
                        |]
                | "magnesium" ->
                    if intake.Magnesium |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Magnesium[0] |> typoGraphy
                            intake.Magnesium[1] |> typoGraphy
                        |]
                | "phosphaat" ->
                    if intake.Phosphate |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Phosphate[0] |> typoGraphy
                            intake.Phosphate[1] |> typoGraphy
                        |]
                | "ijzer" ->
                    if intake.Iron |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Iron[0] |> typoGraphy
                            intake.Iron[1] |> typoGraphy
                        |]
                | "vitamine D" ->
                    if intake.VitaminD |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.VitaminD[0] |> typoGraphy
                            intake.VitaminD[1] |> typoGraphy
                        |]
                | "ethanol" ->
                    if intake.Ethanol |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Ethanol[0] |> typoGraphy
                            intake.Ethanol[1] |> typoGraphy
                        |]
                | "propyleenglycol" ->
                    if intake.Propyleenglycol |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.Propyleenglycol[0] |> typoGraphy
                            intake.Propyleenglycol[1] |> typoGraphy
                        |]
                | "boorzuur" ->
                    if intake.BoricAcid |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.BoricAcid[0] |> typoGraphy
                            intake.BoricAcid[1] |> typoGraphy
                        |]
                | "benzylalcohol" ->
                    if intake.BenzylAlcohol |> Array.isEmpty then [||] |> Array.map box
                    else
                        [|
                            cells[0] |> box
                            intake.BenzylAlcohol[0] |> typoGraphy
                            intake.BenzylAlcohol[1] |> typoGraphy
                        |]
                | _ -> [||] |> Array.map box
            )

        let rows1, rows2, rows3, rows4 =
            match res with
            | Resolved intake ->
                let map = mapRow intake
                map rows1
                ,
                map rows2
                ,
                map rows3
                ,
                map rows4
            | _ ->
                rows1 |> Array.map (Array.map box),
                rows2 |> Array.map (Array.map box),
                rows3 |> Array.map (Array.map box),
                rows4 |> Array.map (Array.map box)

        let content1 =
            Components.BasicTable.View({|
                header = [||]
                rows =rows1
            |})
            |> toReact

        let content2 =
            Components.BasicTable.View({|
                header = [||]
                rows =rows2
            |})
            |> toReact

        let content3 =
            Components.BasicTable.View({|
                header = [||]
                rows =rows3
            |})
            |> toReact

        let content4 =
            Components.BasicTable.View({|
                header = [||]
                rows =rows4
            |})
            |> toReact

        Components.BottomDrawer.View {|
            isOpen = true;
            content = [| content1; content2; content3; content4 |]
            |}
