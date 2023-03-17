namespace Views

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types



module EmergencyList =


    [<JSX.Component>]
    let View (props : {| interventions: Deferred<Shared.Types.Intervention list> |}) =

        let columns = [|
            {|  field = "id"; headerName = "id"; width = 0; filterable = false; sortable = false;  |}
            {|  field = "indication"; headerName = "Indicatie"; width = 200; filterable = true; sortable = true |}
            {|  field = "intervention"; headerName = "Interventie"; width = 200; filterable = true; sortable = true |}
            {|  field = "calculated"; headerName = "Berekend"; width = 200; filterable = false; sortable = false |}
            {|  field = "preparation"; headerName = "Bereiding"; width = 200; filterable = false; sortable = false |} //``type`` = "number"
            {|  field = "advice"; headerName = "Advies"; width = 200; filterable = false; sortable = false |}
        |]


        let speakAct s =
            let speak = fun _ -> s |> Global.Speech.speak
            JSX.jsx
                $"""
            import CardActions from '@mui/material/CardActions';
            import IconButton from '@mui/material/IconButton';

            <CardActions disableSpacing>
                <IconButton onClick={speak}>
                    {Mui.Icons.CampaignIcon}
                </IconButton>
            </CardActions>
            """
            |> toReact
            |> Some

        let repl s =
            s
            |> String.replace "ml" "milli liter"
            |> String.replace "mg" "milli gram"
            |> String.replace "mcg" "micro gram"
            |> String.replace "/" " per "
            |> String.replace " (" ", "
            |> String.replace ")" ""
            |> String.replace "-" " tot, "

        let rows =
            match props.interventions with
            | Resolved items ->
                items
                |> List.toArray
                |> Array.mapi (fun i m ->
                    let b = m.InterventionDoseText |> String.IsNullOrWhiteSpace
                    let sentence =
                        let s = if b then m.SubstanceDoseText |> repl else m.InterventionDoseText |> repl
                        $"{m.Name}, {s}"
                    {|
                        cells =
                            [|
                                {| field = "id"; value = $"{i + 1}" |}
                                {| field = "indication"; value = $"{m.Indication}" |}
                                {| field = "intervention"; value = $"**{m.Name}**" |}
                                {| field = "calculated"; value = (if b then $"*{m.SubstanceDoseText}*" else m.SubstanceDoseText)  |}
                                {| field = "preparation"; value =  (if b then "" else $"*{m.InterventionDoseText}*") |}
                                {| field = "advice"; value = $"{m.Text}" |}
                            |]
                        actions = sentence |> speakAct
                    |}
                )
            | _ -> [||]

        let rowCreate (cells : string []) =
            if cells |> Array.length <> 6 then
                failwith $"cannot create row with {cells}"
            else
                {|
                    id = cells[0]
                    indication = cells[1].Replace("*", "")
                    intervention = cells[2].Replace("*", "")
                    calculated = cells[3].Replace("*", "")
                    preparation = cells[4].Replace("*", "")
                    advice = cells[5].Replace("*", "")
                |}
            |> box

        Components.ResponsiveTable.View({|
            columns = columns
            rows = rows
            rowCreate = rowCreate
        |})


