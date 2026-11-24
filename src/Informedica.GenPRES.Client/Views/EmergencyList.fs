namespace Views


module EmergencyList =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Policies


    open System
    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envList = AppEnv.asEnv<AppEnv.IEmergencyList> props.appEnv
        let filterState = envList.EmergencyListFilter
        let onFilterChange = envList.OnEmergencyListFilterChange

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let patient = (AppEnv.asEnv<AppEnv.IPatient> props.appEnv).Draft

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization

        let printOpen, setPrintOpen = React.useState false
        let printLabels = Global.printLabels ()
        let weightKg = ViewHelpers.PrintView.patientWeight printLabels.unknown patient

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let renderCalculatedCell =
            fun (pars: obj) ->
                let value: string = pars?value
                value |> TextBlockParser.fromString |> Mui.TypoGraphy.fromTextBlock

        let renderPreparationCell =
            fun (pars: obj) ->
                let value: string = pars?value
                value |> TextBlockParser.fromString |> Mui.TypoGraphy.fromTextBlock

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
                        "field" ==> "category"
                        "headerName" ==> (Terms.``Emergency List Category`` |> getTerm "Category")
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
                        ==> (Terms.``Emergency List Intervention`` |> getTerm "Intervention")
                        "width" ==> 300
                        "minWidth" ==> 150
                        "flex" ==> 1
                        "filterable" ==> true
                        "sortable" ==> true
                    ]
                createObj
                    [
                        "field" ==> "calculated"
                        "headerName" ==> (Terms.``Emergency List Calculated`` |> getTerm "Calculation")
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
                        "headerName" ==> (Terms.``Emergency List Preparation`` |> getTerm "Preparation")
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
                        "headerName" ==> (Terms.``Emergency List Advice`` |> getTerm "Advised")
                        "width" ==> 300
                        "minWidth" ==> 150
                        "flex" ==> 1
                        "filterable" ==> false
                        "sortable" ==> false
                    ]
            |]

        // The rows the server calculated for the patient, also while they are calculated again.
        let rows =
            envList.EmergencyList
            |> Deferred.defaultValue [||]
            |> Array.mapi (fun i m ->
                let b = m.InterventionDoseText |> String.isNullOrWhiteSpace

                {|
                    cells =
                        [|
                            {|
                                field = "id"
                                value = $"%i{i + 1}"
                            |}
                            {|
                                field = "category"
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

        let rowCreate (cells: string[]) =
            if cells |> Array.length <> 6 then
                invalidArg (nameof cells) $"cannot create row with {cells}"
            else
                {|
                    id = cells[0]
                    category = cells[1].Replace("*", "")
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
                    field = "category"
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

        let boxSx =
            {|
                height = "100%"
                display = "flex"
                flexDirection = "column"
            |}

        let tableSx =
            {|
                flex = 1
                minHeight = 0
            |}

        let patientNotice =
            Components.PatientNotice.View
                {|
                    appEnv = props.appEnv
                    needs = PatientReadinessPolicy.Needs.Calculation
                |}

        let tableProps =
            {|
                hideFilter = false
                columns = columns
                rows = rows
                rowCreate = rowCreate
                height = "100%"
                onRowClick = ignore
                markedRow = None
                checkboxSelection = false
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
                title = Terms.``Emergency List`` |> getTerm "Emergency List"
                children = printContent
            |}

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import React from 'react';

        <Box sx={boxSx}>
            {patientNotice}
            <Box sx={tableSx}>
                {Components.ResponsiveTable.View tableProps}
            </Box>
            {ViewHelpers.PrintView.PrintDialog printDialogProps}
        </Box>
        """
