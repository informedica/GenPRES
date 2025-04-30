namespace Views

module Intake =


    open Fable.Core
    open Feliz
    open Shared
    open Types


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


    let private typoGraphy (items : Types.TextItem[]) =
        let variant = "body2"
        let print item =
            match item with
            | Normal s ->
                JSX.jsx
                    $"""
                <Typography variant={variant} color={Mui.Colors.Grey.``700``} display="inline">{s}</Typography>
                """
            | Bold s ->
                JSX.jsx
                    $"""
                <Typography
                color={Mui.Colors.BlueGrey.``700``}
                variant={variant}
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
                variant={variant}
                display="inline"
                >
                {s}
                </Typography>
                """

        JSX.jsx
            $"""
        import Typography from '@mui/material/Typography';
        import Box from '@mui/material/Box';

        <Box display="inline" >
            {items |> Array.map print |> unbox |> React.fragment}
        </Box>
        """


    [<JSX.Component>]
    let View(props: {| intake : Totals |}) =
        let mapRow (intake: Totals) row =
            let print n itms =
                if itms |> Array.length < 2 then [||]
                else
                    [|
                        [| Normal n |] |> typoGraphy
                        itms[0..(itms.Length - 2)] |> typoGraphy
                        [| itms |> Array.last |] |> typoGraphy
                    |]
                |> Array.map box

            row
            |> Array.map (fun cells ->
                match cells |> Array.head with
                | "volume"      -> print "volume" intake.Volume
                | "energie"     -> print "energie" intake.Energy
                | "koolhydraat" -> print "koolhydraat" intake.Carbohydrate
                | "eiwit"       -> print "eiwit" intake.Protein
                | "vet"         -> print "vet" intake.Fat
                | "natrium"     -> print "natrium" intake.Sodium
                | "kalium"      -> print "kalium" intake.Potassium
                | "chloride"    -> print "chloride" intake.Chloride
                | "calcium"     -> print "calcium" intake.Calcium
                | "magnesium"   -> print "magnesium" intake.Magnesium
                | "phosphaat"   -> print "phosphaat" intake.Phosphate
                | "ijzer"       -> print "ijzer" intake.Iron
                | "vitamine D"  -> print "vitamine D" intake.VitaminD
                | "ethanol"         -> print "ethanol" intake.Ethanol
                | "propyleenglycol" -> print "propyleenglycol" intake.Propyleenglycol
                | "boorzuur"        -> print "boorzuur" intake.BoricAcid
                | "benzylalcohol"   -> print "benzylalcohol" intake.BenzylAlcohol
                | _ -> [||] |> Array.map box
            )

        let rows1, rows2, rows3, rows4 =
            let map = mapRow props.intake
            map rows1
            ,
            map rows2
            ,
            map rows3
            ,
            map rows4

        let createTable n rows = $"table{n}", Components.BasicTable.View({| header = [||]; rows = rows |}) |> toReact

        let content =
            [|
                if rows1 |> Array.isEmpty |> not then createTable 1 rows1
                if rows2 |> Array.isEmpty |> not then createTable 2 rows2
                if rows3 |> Array.isEmpty |> not then createTable 3 rows3
                if rows4 |> Array.isEmpty |> not then createTable 4 rows4
            |]

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        if isMobile then
            JSX.jsx $"""
            import React from 'react';
            <React.Fragment />
            """
        else
            Components.BottomDrawer.View {|
                isOpen = true;
                content = content
                |}
