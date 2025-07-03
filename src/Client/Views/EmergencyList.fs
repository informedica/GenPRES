namespace Views


module EmergencyList =


    open System
    open Fable.Core
    open Feliz
    open Shared


    [<JSX.Component>]
    let View (props : {| interventions: Deferred<Types.Intervention list>; localizationTerms : Deferred<string [] []> |}) =

        let context = React.useContext(Global.context)
        let lang = context.Localization
        let hosp = context.Hospital

        let getTerm defVal term =
            props.localizationTerms
            |> Deferred.map (fun terms ->
                Localization.getTerm terms lang term
                |> Option.defaultValue defVal
            )
            |> Deferred.defaultValue defVal

        let columns = [|
            {|  field = "id"; headerName = "id"; width = 0; filterable = false; sortable = false;  |}
            {|  field = "catagory"; headerName = Terms.``Emergency List Catagory`` |> getTerm "Category"; width = 200; filterable = true; sortable = true |}
            {|  field = "intervention"; headerName = Terms.``Emergency List Intervention`` |> getTerm "Interventie"; width = 200; filterable = true; sortable = true |}
            {|  field = "calculated"; headerName = Terms.``Emergency List Calculated`` |> getTerm "Berekend"; width = 200; filterable = false; sortable = false |}
            {|  field = "preparation"; headerName = Terms.``Emergency List Preparation`` |> getTerm "Bereiding"; width = 200; filterable = false; sortable = false |} //``type`` = "number"
            {|  field = "advice"; headerName = Terms.``Emergency List Advice`` |> getTerm "Advies"; width = 400; filterable = false; sortable = false |}
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
                |> List.filter (fun item ->
                    hosp |> String.isNullOrWhiteSpace ||
                    item.Hospital |> String.isNullOrWhiteSpace ||
                    hosp = item.Hospital
                )
                |> List.distinctBy (fun item -> 
                    item.Catagory, item.Name, item.InterventionDoseText)
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
                                {| field = "catagory"; value = $"{m.Catagory}" |}
                                {| field = "intervention"; value = $"**{m.Name}**" |}
                                {| field = "calculated"; value = if b then $"*{m.SubstanceDoseText}*" else m.SubstanceDoseText  |}
                                {| field = "preparation"; value =  if b then "" else $"*{m.InterventionDoseText}*" |}
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
                    catagory = cells[1].Replace("*", "")
                    intervention = cells[2].Replace("*", "")
                    calculated = cells[3].Replace("*", "")
                    preparation = cells[4].Replace("*", "")
                    advice = cells[5].Replace("*", "")
                |}
            |> box

        Components.ResponsiveTable.View({|
            hideFilter = false
            columns = columns
            rows = rows
            rowCreate = rowCreate
            height = "70vh"
            onRowClick = ignore
            checkboxSelection = false
            selectedRows = [||]
            onSelectChange = ignore
        |})


