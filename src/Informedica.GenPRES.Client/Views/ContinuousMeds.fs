namespace Views


module ContinuousMeds =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Policies

    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envList = AppEnv.asEnv<AppEnv.IContinuousList> props.appEnv
        let filterState = envList.ContinuousListFilter
        let onFilterChange = envList.OnContinuousListFilterChange

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let patient = (AppEnv.asEnv<AppEnv.IPatient> props.appEnv).Draft

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization

        let printOpen, setPrintOpen = React.useState false
        let printLabels = Global.printLabels ()
        let weightKg = ViewHelpers.PrintView.patientWeight printLabels.unknown patient

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let renderQuantityCell =
            fun (pars: obj) ->
                let value: string = pars?value
                value |> TextBlockParser.fromString |> Mui.TypoGraphy.fromTextBlock

        let renderSolutionCell =
            fun (pars: obj) ->
                let value: string = pars?value
                value |> TextBlockParser.fromString |> Mui.TypoGraphy.fromTextBlock

        let columns =
            [|
                {|
                    field = "id"
                    headerName = "id"
                    width = 0
                    filterable = false
                    sortable = false
                |}
                |> box
                {|
                    field = "category"
                    headerName = Terms.``Continuous Medication Category`` |> getTerm "Category"
                    width = 140
                    filterable = true
                    sortable = true
                |}
                |> box
                {|
                    field = "medication"
                    headerName = Terms.``Continuous Medication Medication`` |> getTerm "Medication"
                    width = 200
                    filterable = true
                    sortable = true
                |}
                |> box
                createObj
                    [
                        "field" ==> "quantity"
                        "headerName"
                        ==> (Terms.``Continuous Medication Quantity`` |> getTerm "Quantity")
                        "width" ==> 140
                        "filterable" ==> false
                        "sortable" ==> false
                        "renderCell" ==> renderQuantityCell
                    ]
                createObj
                    [
                        "field" ==> "solution"
                        "headerName"
                        ==> (Terms.``Continuous Medication Solution`` |> getTerm "Solution")
                        "width" ==> 140
                        "filterable" ==> false
                        "sortable" ==> false
                        "renderCell" ==> renderSolutionCell
                    ]
                {|
                    field = "dose"
                    headerName = Terms.``Continuous Medication Dose`` |> getTerm "Dose"
                    width = 200
                    filterable = false
                    sortable = false
                |}
                |> box
                {|
                    field = "advice"
                    headerName = Terms.``Continuous Medication Advice`` |> getTerm "Advice"
                    width = 200
                    filterable = false
                    sortable = false
                |}
                |> box
            |]

        // The table gives the id of the clicked row, which is its number in the list.
        let handleRowClick (id: string) = id |> int |> envList.OnContinuousListRowChosen

        // The table gives the ids of the checked rows, which are their numbers in the list.
        let handleSelectChange (ids: string[]) = ids |> Array.map int |> envList.OnContinuousListRowsChecked

        let selectedRows = envList.ContinuousListChecked |> Array.map string

        // The button is disabled while no row is checked.
        let prescribeAction =
            Components.ActionBar.prescribe
                getTerm
                envList.OnContinuousListPrescribe
                (envList.ContinuousListCheckedContexts |> Array.isEmpty)

        let prescribeButton = Components.ActionBar.ActionButton prescribeAction

        // The rows the server calculated for the patient, also while they are calculated again.
        let rows =
            envList.ContinuousList
            |> Deferred.defaultValue [||]
            |> Array.mapi (fun i m ->
                {|
                    cells =
                        [|
                            {|
                                field = "id"
                                value = $"%i{i + 1}"
                            |}
                            {|
                                field = "category"
                                value = m.Category
                            |}
                            {|
                                field = "medication"
                                value = $"**%s{m.Name}**"
                            |}
                            {|
                                field = "quantity"
                                value = m.QuantityUnit
                            |}
                            {|
                                field = "solution"
                                value = m.Solution
                            |}
                            {|
                                field = "dose"
                                value = m.SubstanceDoseText
                            |}
                            {|
                                field = "advice"
                                value = m.Text
                            |}
                        |]
                    actions = None
                |}
            )

        let rowCreate (cells: string[]) =
            if cells |> Array.length <> 7 then
                invalidArg (nameof cells) $"cannot create row with {cells}"
            else
                {|
                    id = cells[0]
                    category = cells[1].Replace("*", "")
                    medication = cells[2].Replace("*", "")
                    quantity = cells[3].Replace("*", "")
                    solution = cells[4].Replace("*", "")
                    dose = cells[5].Replace("*", "")
                    advice = cells[6].Replace("*", "")
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
                    field = "medication"
                    label = "Medicatie"
                    width = "20%"
                |}
                {|
                    field = "quantity"
                    label = "Hoeveelheid"
                    width = "15%"
                |}
                {|
                    field = "solution"
                    label = "Oplossing"
                    width = "15%"
                |}
                {|
                    field = "dose"
                    label = "Dosering"
                    width = "20%"
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
                onRowClick = handleRowClick
                markedRow = None
                checkboxSelection = true
                selectedRows = selectedRows
                onSelectChange = handleSelectChange
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
                actions = Some prescribeButton
            |}

        let printDialogProps =
            {|
                isOpen = printOpen
                onClose = fun () -> setPrintOpen false
                title = Terms.``Continuous Medication List`` |> getTerm "Continuous Medication"
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
