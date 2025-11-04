namespace Views


module EmergencyList =


    open System
    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Shared
    open Shared.Types


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

        let renderCalculatedCell =
            fun (pars: obj) ->
                let value: string = pars?value
                value 
                |> TextBlock.fromString 
                |> Mui.TypoGraphy.fromTextBlock

        let renderPreparationCell =
            fun (pars: obj) ->
                let value: string = pars?value
                value 
                |> TextBlock.fromString 
                |> Mui.TypoGraphy.fromTextBlock

        let columns = [|
            {|  field = "id"; headerName = "id"; width = 0; filterable = false; sortable = false;  |} |> box
            {|  field = "catagory"; headerName = Terms.``Emergency List Catagory`` |> getTerm "Category"; width = 140; filterable = true; sortable = true |} |> box
            {|  field = "intervention"; headerName = Terms.``Emergency List Intervention`` |> getTerm "Interventie"; width = 300; filterable = true; sortable = true |} |> box
            createObj [
                "field" ==> "calculated"
                "headerName" ==> (Terms.``Emergency List Calculated`` |> getTerm "Berekend")
                "width" ==> 180
                "filterable" ==> false
                "sortable" ==> false
                "renderCell" ==> renderCalculatedCell
            ]
            createObj [
                "field" ==> "preparation"
                "headerName" ==> (Terms.``Emergency List Preparation`` |> getTerm "Bereiding")
                "width" ==> 180
                "filterable" ==> false
                "sortable" ==> false
                "renderCell" ==> renderPreparationCell
            ]
            {|  field = "advice"; headerName = Terms.``Emergency List Advice`` |> getTerm "Advies"; width = 300; filterable = false; sortable = false |} |> box
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
                    item.Category, item.Name, item.InterventionDoseText)
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
                                {| field = "catagory"; value = $"{m.Category}" |}
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
            showToolbar = true
            showFooter = true
        |})


