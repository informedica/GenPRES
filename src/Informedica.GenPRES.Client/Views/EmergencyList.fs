namespace Views


module EmergencyList =


    open System
    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Shared
    open Shared.Types


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envBolus = AppEnv.asEnv<AppEnv.IBolusMedication> props.appEnv
        let interventions = envBolus.BolusMedication
        let onSelectItem = envBolus.OnSelectBolusMedicationItem
        let filterState = envBolus.BolusMedicationFilter
        let onFilterChange = envBolus.OnBolusMedicationFilterChange

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let patient = (AppEnv.asEnv<AppEnv.IPatient> props.appEnv).Draft

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let hosp = context.Hospital

        let printOpen, setPrintOpen = React.useState false
        let printLabels = Global.printLabels ()
        let weightKg = ViewHelpers.PrintView.patientWeight printLabels.unknown patient

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let renderCalculatedCell =
            fun (pars: obj) ->
                let value: string = pars?value
                value |> TextBlock.fromString |> Mui.TypoGraphy.fromTextBlock

        let renderPreparationCell =
            fun (pars: obj) ->
                let value: string = pars?value
                value |> TextBlock.fromString |> Mui.TypoGraphy.fromTextBlock

        let columns =
            [|
                // id column: hidden via columnVisibilityModel
                createObj
                    [
                        "field" ==> "id"
                        "headerName" ==> "id"
                        "width" ==> 0
                        "filterable" ==> false
                        "sortable" ==> false
                    ]
                // flex distributes available width proportionally; minWidth prevents collapse
                createObj
                    [
                        "field" ==> "catagory"
                        "headerName" ==> (Terms.``Emergency List Catagory`` |> getTerm "Category")
                        "width" ==> 140
                        "minWidth" ==> 100
                        "flex" ==> 0.5
                        "filterable" ==> true
                        "sortable" ==> true
                    ]
                createObj
                    [
                        "field" ==> "intervention"
                        "headerName"
                        ==> (Terms.``Emergency List Intervention`` |> getTerm "Interventie")
                        "width" ==> 300
                        "minWidth" ==> 150
                        "flex" ==> 1
                        "filterable" ==> true
                        "sortable" ==> true
                    ]
                createObj
                    [
                        "field" ==> "calculated"
                        "headerName" ==> (Terms.``Emergency List Calculated`` |> getTerm "Berekend")
                        "width" ==> 180
                        "minWidth" ==> 120
                        "flex" ==> 0.8
                        "filterable" ==> false
                        "sortable" ==> false
                        "renderCell" ==> renderCalculatedCell
                    ]
                createObj
                    [
                        "field" ==> "preparation"
                        "headerName" ==> (Terms.``Emergency List Preparation`` |> getTerm "Bereiding")
                        "width" ==> 180
                        "minWidth" ==> 120
                        "flex" ==> 0.8
                        "filterable" ==> false
                        "sortable" ==> false
                        "renderCell" ==> renderPreparationCell
                    ]
                createObj
                    [
                        "field" ==> "advice"
                        "headerName" ==> (Terms.``Emergency List Advice`` |> getTerm "Advies")
                        "width" ==> 300
                        "minWidth" ==> 150
                        "flex" ==> 1
                        "filterable" ==> false
                        "sortable" ==> false
                    ]
            |]

        let speakAct s =
            let speak = fun _ -> s |> Global.Speech.speak

            JSX.jsx
                $"""
            import CardActions from '@mui/material/CardActions';
            import IconButton from '@mui/material/IconButton';

            <CardActions disableSpacing>
                <IconButton onClick={speak} aria-label="Read aloud">
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
            match interventions with
            | Resolved items ->
                items
                |> List.filter (fun item ->
                    hosp |> String.isNullOrWhiteSpace
                    || item.Hospital |> String.isNullOrWhiteSpace
                    || hosp = item.Hospital
                )
                |> List.distinctBy (fun item -> item.Category, item.Name, item.InterventionDoseText)
                |> List.toArray
                |> Array.mapi (fun i m ->
                    let b = m.InterventionDoseText |> String.isNullOrWhiteSpace

                    let sentence =
                        let s =
                            if b then
                                m.SubstanceDoseText |> repl
                            else
                                m.InterventionDoseText |> repl

                        $"{m.Name}, {s}"

                    {|
                        cells =
                            [|
                                {|
                                    field = "id"
                                    value = $"{i + 1}.{m.Hospital}.{m.Category}.{m.Name}"
                                |}
                                {|
                                    field = "catagory"
                                    value = $"{m.Category}"
                                |}
                                {|
                                    field = "intervention"
                                    value = $"**{m.Name}**"
                                |}
                                {|
                                    field = "calculated"
                                    value =
                                        if b then
                                            $"*{m.SubstanceDoseText}*"
                                        else
                                            m.SubstanceDoseText
                                |}
                                {|
                                    field = "preparation"
                                    value = if b then "" else $"*{m.InterventionDoseText}*"
                                |}
                                {|
                                    field = "advice"
                                    value = $"{m.Text}"
                                |}
                            |]
                        actions = None
                    |}
                )
            | _ -> [||]

        let rowCreate (cells: string[]) =
            if cells |> Array.length <> 6 then
                invalidArg (nameof cells) $"cannot create row with {cells}"
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

        let printData, setPrintData = React.useState [||]

        let patientHeader =
            ViewHelpers.PrintView.PatientHeader
                {|
                    weightKg = weightKg
                    labels = printLabels
                |}

        let patientSignature = ViewHelpers.PrintView.PatientSignature {| label = printLabels.signature |}

        let printColumns =
            [|
                {|
                    field = "catagory"
                    label = "Categorie"
                    width = "15%"
                |}
                {|
                    field = "intervention"
                    label = "Interventie"
                    width = "20%"
                |}
                {|
                    field = "calculated"
                    label = "Berekend"
                    width = "25%"
                |}
                {|
                    field = "preparation"
                    label = "Bereiding"
                    width = "25%"
                |}
                {|
                    field = "advice"
                    label = "Advies"
                    width = "15%"
                |}
            |]

        let printContent =
            Components.PrintTable.View
                {|
                    columns = printColumns
                    rows = printData
                    header = patientHeader
                    signature = patientSignature
                |}
            |> toReact

        let boxSx = {| height = "100%" |}

        let tableProps =
            {|
                hideFilter = false
                columns = columns
                rows = rows
                rowCreate = rowCreate
                height = "100%"
                onRowClick = onSelectItem
                checkboxSelection = false
                selectDisabled = false
                selectedRows = [||]
                onSelectChange = ignore
                showToolbar = true
                showFooter = true
                onPrint =
                    Some(fun filteredRows ->
                        setPrintData filteredRows
                        setPrintOpen true
                    )
                selectedFilter = Some filterState
                onFilterChange = Some onFilterChange
                filterLabel = "Filter"
                searchLabel = "Zoek"
            |}

        let printDialogProps =
            {|
                isOpen = printOpen
                onClose = fun () -> setPrintOpen false
                title = Terms.``Emergency List`` |> getTerm "Noodlijst"
                children = printContent
            |}

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import React from 'react';

        <Box sx={boxSx}>
            {Components.ResponsiveTable.View tableProps}
            {ViewHelpers.PrintView.PrintDialog printDialogProps}
        </Box>
        """
