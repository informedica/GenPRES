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
                        "field" ==> "route"
                        "headerName" ==> (Terms.Route |> getTerm "Route")
                        "width" ==> 140
                        "minWidth" ==> 100
                        "flex" ==> 0.5
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
        let interventions = envList.EmergencyList |> Deferred.defaultValue [||]

        // The table gives the id of the clicked row, which is its number in the list.
        let handleRowClick (id: string) = id |> int |> envList.OnEmergencyListRowChosen

        // The table gives the ids of the checked rows, which are their numbers in the list.
        let handleSelectChange (ids: string[]) = ids |> Array.map int |> envList.OnEmergencyListRowsChecked

        let selectedRows = envList.EmergencyListChecked |> Array.map string

        // The button is disabled while no checked row has an order context.
        let prescribeAction =
            Components.ActionBar.prescribe
                getTerm
                envList.OnEmergencyListPrescribe
                (envList.EmergencyListCheckedContexts |> Array.isEmpty)

        let prescribeButton = Components.ActionBar.ActionButton prescribeAction

        // a medication row has an order context narrowed to the scenario of the row; a calculated intervention
        // has none, so its route and its details are empty
        let scenarioOf (m: Intervention) =
            m.OrderContext |> Option.bind (fun ctx -> ctx.Scenarios |> Array.tryHead)

        let routeOf m = m |> scenarioOf |> Option.map _.Route |> Option.defaultValue ""

        // the number of rows with the same category, intervention and route
        let sameRows =
            interventions
            |> Array.countBy (fun m -> m.Category, m.Name, routeOf m)
            |> Map.ofArray

        let rows =
            interventions
            |> Array.mapi (fun i m ->
                let b = m.InterventionDoseText |> String.isNullOrWhiteSpace

                let route = routeOf m

                // the dose type and the access after the name set apart the rows with the same category,
                // intervention and route, such as the first and the second dose of amiodaron; a row without
                // such a row shows the name alone
                let details =
                    match m |> scenarioOf with
                    | Some sc when sameRows[m.Category, m.Name, route] > 1 ->
                        let tube = Terms.``Patient Enteral Tube`` |> getTerm "Enteral tube"

                        let texts =
                            [
                                sc.DoseType |> DoseTypeText.text
                                sc.Access |> Option.map (PatientText.accessText tube)
                            ]
                            |> List.choose id

                        match texts with
                        | [] -> ""
                        | _ ->
                            let joined = texts |> String.concat ", "
                            $" (%s{joined})"
                    | _ -> ""

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
                                value = $"**%s{m.Name}**%s{details}"
                            |}
                            {|
                                field = "route"
                                value = route
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
            if cells |> Array.length <> 7 then
                invalidArg (nameof cells) $"cannot create row with {cells}"
            else
                {|
                    id = cells[0]
                    category = cells[1].Replace("*", "")
                    intervention = cells[2].Replace("*", "")
                    route = cells[3]
                    calculated = cells[4].Replace("*", "")
                    preparation = cells[5].Replace("*", "")
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
                    width = "17%"
                |}
                {|
                    field = "intervention"
                    label = "Interventie"
                    width = "18%"
                |}
                {|
                    field = "route"
                    label = "Route"
                    width = "17%"
                |}
                {|
                    field = "calculated"
                    label = "Berekend"
                    width = "16%"
                |}
                {|
                    field = "preparation"
                    label = "Bereiding"
                    width = "16%"
                |}
                {|
                    field = "advice"
                    label = "Advies"
                    width = "16%"
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
