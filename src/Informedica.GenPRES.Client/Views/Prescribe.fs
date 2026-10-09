namespace Views


module Prescribe =

    open Fable.Core
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models
    open OrderPlanMachine
    open OrderContextMachine


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envOrderContext = AppEnv.asEnv<AppEnv.IOrderContext> props.appEnv
        let orderContext = envOrderContext.OrderContext
        let orderContextMsg = envOrderContext.OrderContextMsg
        let envOrderPlan = AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv
        let orderPlan = envOrderPlan.OrderPlan
        let draft = (AppEnv.asEnv<AppEnv.IPatient> props.appEnv).Draft

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let isMobile = Mui.Hooks.useMediaQuery "(max-width:900px)"

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        // a pick goes as its position in the options the field offers, an emptied field as a
        // clear; a value the field does not offer is a bug, written to the console and not sent
        let pickFilter (options: OrderContext -> 'a[]) (picked: 'a option) toCommand =
            match orderContext with
            | OrderContextView.Settled pr
            | OrderContextView.Refused(pr, _) ->
                match picked |> Option.map (fun x -> pr |> options |> Array.tryFindIndex ((=) x)) with
                | None -> orderContextMsg (toCommand None)
                | Some(Some n) -> orderContextMsg (toCommand (Some n))
                | Some None -> Logging.warning "a pick the field does not offer is not sent" picked
            | _ -> ()

        let filterCommand field n =
            match n with
            | Some n -> Api.OrderViewCommand.SetNthFilterProperty(field, n)
            | None -> Api.OrderViewCommand.ClearFilterProperty field

        let indicationChange s =
            filterCommand OrderContext.Indication |> pickFilter _.Filter.Indications s

        let medicationChange s =
            filterCommand OrderContext.Generic |> pickFilter _.Filter.Generics s

        let routeChange s = filterCommand OrderContext.Route |> pickFilter _.Filter.Routes s

        let formChange s = filterCommand OrderContext.Form |> pickFilter _.Filter.Forms s

        let diluentCommand n =
            match n with
            | Some n -> Api.OrderViewCommand.SetNthDiluentProperty n
            | None -> Api.OrderViewCommand.ClearDiluentProperty

        let diluentChange s = diluentCommand |> pickFilter _.Filter.Diluents s

        let componentsChange (cs: string[]) =
            match orderContext with
            | OrderContextView.Settled pr
            | OrderContextView.Refused(pr, _) ->
                let ns = cs |> Array.choose (fun c -> pr.Filter.Components |> Array.tryFindIndex ((=) c))

                if ns.Length = cs.Length then
                    orderContextMsg (Api.OrderViewCommand.SetNthComponentsProperty ns)
                else
                    Logging.warning "components the field does not offer are not sent" cs
            | _ -> ()

        let doseTypeChange s =
            filterCommand OrderContext.DoseType
            |> pickFilter _.Filter.DoseTypes (s |> Option.map DoseType.doseTypeFromString)

        let clear () =
            match orderContext with
            | OrderContextView.Settled _
            | OrderContextView.Refused _ -> orderContextMsg Api.OrderViewCommand.ClearAllFilterProperty
            | _ -> ()

        // the dialog is open while a scenario is selected: the selection is the state
        let dialog = envOrderContext.Dialog
        let handleModalClose = fun () -> envOrderContext.SelectScenario None

        // the filter is put back as it was: bounded and to the left, since it discards what the
        // user built and must not be where you click by default
        let resetBar =
            Components.ActionBar.View
                {|
                    actions =
                        [|
                            {|
                                label = Reset |> getTerm "Reset"
                                kind = Components.ActionBar.Kind.Secondary
                                onClick = clear
                                disabled = false
                                icon = Some Mui.Icons.RefreshIcon
                            |}
                        |]
                |}


        // the page is disabled as a whole while the workbench changes, so no field greys itself
        let select = ViewHelpers.filterSelect

        let multiSelect lbl selected dispatch xs =
            Components.MultiPickField.View
                {|
                    label = lbl
                    options = xs
                    selected = selected
                    onChange = dispatch
                    isLoading = false
                    enabled = true
                |}

        let responsiveFilter = ViewHelpers.responsiveFilter isMobile

        // what the patient data misses for the dose rules, said above the selects while it
        // holds: no patient yet, or one without a weight or a height, measured or estimated, or
        // without an age, which loses every dose rule with an age bound
        let notice =
            Components.PatientNotice.View
                {|
                    appEnv = props.appEnv
                    needs = PatientReadiness.Needs.Calculation
                |}

        let noticeSx = {| margin = 1 |}

        // the server refused the picks: no dose can be shown, and the page says why, with the
        // picks kept above it for the user to change. A patient without a weight or a height
        // gets no second notice: the one above already says what to enter
        let refusalNotice =
            match orderContext with
            | OrderContextView.Refused(ctx, refusal) ->
                let tr (term: Terms) = term |> getTerm (OrderContextRefusalPolicy.english term)

                match OrderContextRefusalPolicy.notice tr ctx refusal with
                | None -> null
                | Some words ->
                    let notice =
                        Components.Notice.View
                            {|
                                kind = Components.Notice.Kind.Warning
                                title = Some words.Title
                                message = words |> OrderContextRefusalPolicy.message
                                action = None
                                onClose = None
                            |}

                    JSX.jsx
                        $"""
                    <Box sx={noticeSx}>{notice}</Box>
                    """
            | _ -> null

        let displayScenario (pr: OrderContext) med (sc: OrderScenario) =
            if med |> Option.isNone then
                null
            else
                let caption =
                    // the access the preparation is for, labelled as the patient panel labels it
                    let access =
                        sc.Access
                        |> Option.map (
                            function
                            | CVL -> "CVL"
                            | PVL -> "PVL"
                            | EnteralTube -> Terms.``Patient Enteral Tube`` |> getTerm "Sonde"
                        )
                        |> Option.map (fun s -> $" (%s{s})")
                        |> Option.defaultValue ""

                    let renal =
                        sc.RenalRule
                        |> Option.map (fun s -> $" (doseer aanpassing volgens {s})")
                        |> Option.defaultValue ""

                    $"{sc.Form}{access}{renal}"

                let onClick (sc: OrderScenario) =
                    match pr.Scenarios |> Array.tryFindIndex (fun x -> x.Order.Id = sc.Order.Id) with
                    | Some n -> orderContextMsg (Api.OrderViewCommand.SelectNthOrderScenario n)
                    | None -> ()

                // the order into the plan, from the workbench narrowed to its scenario; the page
                // switches to the plan when the server answers
                let prescribe () =
                    match orderPlan with
                    | OrderPlanView.Settled _ -> envOrderPlan.Add sc.Order.Id
                    | _ -> ()

                // the plan holds this order already: the server would refuse it
                let inPlan = orderPlan |> OrderPlanView.holds sc.Order.Id

                // one change to the plan at a time: while it is busy a click would be dropped
                let planBusy =
                    match orderPlan with
                    | OrderPlanView.Settled _ -> false
                    | OrderPlanView.NoPatient
                    | OrderPlanView.Changing _ -> true

                let prescribeDisabled = planBusy || inPlan

                // the scenario selected, and the workbench narrowed to it and calculated
                let handleEditClick () =
                    envOrderContext.SelectScenario(Some sc.Order.Id)
                    onClick sc

                let cellSx =
                    {|
                        paddingTop = 1
                        paddingRight = 2
                    |}

                let tableContainerSx = {| overflowX = "auto" |}

                let tableSx =
                    {|
                        tableLayout = "auto"
                        width = "auto"
                    |}

                let tableRowSx =
                    {|
                        border = 0
                        ``& td`` = {| borderBottom = 0 |}
                    |}

                let headerSx =
                    {|
                        backgroundColor = Mui.Styles.headerBgColor
                        padding = 1
                        borderRadius = 1
                    |}

                let listSx =
                    {|
                        width = "100%"
                        maxWidth = 1200
                        bgcolor = Mui.Colors.Grey.``50``
                    |}

                let item key icon prim (sec: TextBlock[][]) =
                    let rows =
                        let cells row =
                            row
                            |> Array.mapi (fun i cell ->
                                JSX.jsx
                                    $"""
                                    <TableCell key={i} sx={cellSx}>
                                        {cell |> Mui.TypoGraphy.fromTextBlock}
                                    </TableCell>
                                    """
                            )

                        let sec = if not isMobile then sec else sec |> TextBlock.flatten

                        sec
                        |> Array.mapi (fun i row ->
                            JSX.jsx
                                $"""
                                <TableRow key={i} sx={ {| border = 0 |} } >
                                    {cells row}
                                </TableRow>
                            """
                        )

                    JSX.jsx
                        $"""
                    import Table from '@mui/material/Table';
                    import TableBody from '@mui/material/TableBody';
                    import TableCell from '@mui/material/TableCell';
                    import TableContainer from '@mui/material/TableContainer';
                    import TableRow from '@mui/material/TableRow';

                    <ListItem key={key} >
                        <ListItemIcon>
                            {icon}
                        </ListItemIcon>
                        <TableContainer sx={tableContainerSx} >
                            <Table padding="none" size="small" sx={tableSx} >
                                <TableBody>
                                    <TableRow sx={tableRowSx} >
                                        <TableCell >
                                            {prim}
                                        </TableCell>
                                    </TableRow >
                                    {rows}
                                </TableBody>
                            </Table>
                        </TableContainer>
                    </ListItem>
                    """

                let content =
                    JSX.jsx
                        $"""
                    <React.Fragment>
                        <Typography variant="h6" sx={headerSx} >
                            {caption}
                        </Typography>
                        <List sx={listSx}>
                            {[|
                                 item "prescription" Mui.Icons.Notes (Terms.``Prescribe Prescription`` |> getTerm "Voorschrift") sc.Prescription
                                 if sc.Preparation |> Array.length > 0 then
                                     item "preparation" Mui.Icons.Vaccines (Terms.``Prescribe Preparation`` |> getTerm "Bereiding") sc.Preparation
                                 item
                                     "administration"
                                     Mui.Icons.MedicationLiquid
                                     (Terms.``Prescribe Administration`` |> getTerm "Toediening")
                                     sc.Administration
                             |]
                             |> unbox<seq<ReactElement>>
                             |> React.Fragment}
                        </List>
                    </React.Fragment>
                    """

                JSX.jsx
                    $"""
                import React from 'react';
                import Card from '@mui/material/Card';
                import CardActions from '@mui/material/CardActions';
                import CardContent from '@mui/material/CardContent';
                import Button from '@mui/material/Button';
                import Typography from '@mui/material/Typography';
                import Box from '@mui/material/Box';
                import List from '@mui/material/List';
                import ListItem from '@mui/material/ListItem';
                import Divider from '@mui/material/Divider';
                import ListItemText from '@mui/material/ListItemText';
                import ListItemIcon from '@mui/material/ListItemIcon';
                import Avatar from '@mui/material/Avatar';
                import Typography from '@mui/material/Typography';

                <Box sx={ {| height = "100%" |} } >
                    <Card sx={ {| padding = 0 |} }>
                        <CardContent sx={ {| padding = 0 |} }>
                            {content}
                        </CardContent>
                        <CardActions>
                            <Button
                                size="small"
                                onClick={handleEditClick}
                                startIcon={Mui.Icons.CalculateIcon}
                            >{Edit |> getTerm "bewerken"}</Button>
                            <Button
                                size="small"
                                disabled={prescribeDisabled}
                                onClick={prescribe}
                                startIcon={Mui.Icons.Add}
                            >Voorschrijven</Button>
                        </CardActions>
                    </Card>
                </Box>
                """

        let stackDirection = if isMobile then "column" else "row"

        let scenarios =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            <Stack direction="column" spacing={1} >
                {match orderContext with
                 | OrderContextView.Settled pr
                 | OrderContextView.Refused(pr, _)
                 | OrderContextView.Changing pr ->
                     pr.Scenarios
                     |> Array.map (displayScenario pr pr.Filter.Generic)
                     |> unbox<seq<ReactElement>>
                     |> React.Fragment
                 | OrderContextView.NoPatient -> Seq.empty<ReactElement> |> React.Fragment}
            </Stack>
            """

        let cards =
            JSX.jsx
                $"""
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';

            <React.Fragment>
                <Stack direction="column" spacing={if isMobile then 1 else 3}>
                    <Typography sx={ {| fontSize = 14 |} } color="text.secondary" >
                        {Terms.``Prescribe Scenarios`` |> getTerm "Medicatie scenario's"}
                    </Typography>
                    {match orderContext with
                     | OrderContextView.Settled pr
                     | OrderContextView.Refused(pr, _)
                     | OrderContextView.Changing pr -> pr.Filter.Indication, pr.Filter.Indications
                     | OrderContextView.NoPatient -> None, [||]
                     |> fun (sel, items) ->
                         let lbl = Terms.``Prescribe Indications`` |> getTerm "Indicaties"

                         items |> responsiveFilter lbl sel indicationChange}
                    <Stack direction={stackDirection} spacing={if isMobile then 1 else 3} >
                        {match orderContext with
                         | OrderContextView.Settled pr
                         | OrderContextView.Refused(pr, _)
                         | OrderContextView.Changing pr -> pr.Filter.Generic, pr.Filter.Generics
                         | OrderContextView.NoPatient -> None, [||]
                         |> fun (sel, items) ->
                             let lbl = Terms.``Prescribe Medications`` |> getTerm "Medicatie"

                             items |> responsiveFilter lbl sel medicationChange

                }
                        {match orderContext with
                         | OrderContextView.Settled pr
                         | OrderContextView.Refused(pr, _)
                         | OrderContextView.Changing pr -> pr.Filter.Route, pr.Filter.Routes
                         | OrderContextView.NoPatient -> None, [||]
                         |> fun (sel, items) ->
                             let lbl = Terms.``Prescribe Routes`` |> getTerm "Routes"

                             items |> responsiveFilter lbl sel routeChange

                }
                        {match orderContext with
                         | OrderContextView.Settled ctx
                         | OrderContextView.Refused(ctx, _)
                         | OrderContextView.Changing ctx when
                             ctx.Filter.Forms |> Array.length >= 1
                             && (not isMobile || ctx.Scenarios |> Array.length <> 1)
                             ->
                             ctx.Filter.Form, ctx.Filter.Forms
                         | _ -> None, [||]
                         |> fun (sel, items) ->
                             let lbl = Terms.Form |> getTerm "Vorm"

                             if items |> Array.isEmpty then
                                 null
                             else if isMobile then
                                 items |> Array.map (fun s -> s, s) |> select lbl sel formChange
                             else
                                 items |> Array.map (fun s -> s, s) |> select lbl sel formChange}
                        {match orderContext with
                         | OrderContextView.Settled pr
                         | OrderContextView.Refused(pr, _)
                         | OrderContextView.Changing pr when
                             pr.Filter.Indication.IsSome
                             && pr.Filter.Generic.IsSome
                             && pr.Filter.Route.IsSome
                             && pr.Filter.Diluents |> Array.length > 1
                             && pr.Scenarios |> Array.length = 1
                             ->

                             let sel = pr.Filter.Diluent
                             let items = pr.Filter.Diluents
                             let lbl = Terms.Diluent |> getTerm "Verdunningsvorm"

                             items |> Array.map (fun s -> s, s) |> select lbl sel diluentChange

                         | _ -> null}
                        {match orderContext with
                         | OrderContextView.Settled pr
                         | OrderContextView.Refused(pr, _)
                         | OrderContextView.Changing pr when
                             pr.Filter.Indication.IsSome
                             && pr.Filter.Generic.IsSome
                             && pr.Filter.Route.IsSome
                             && pr.Filter.Components |> Array.length > 1
                             && pr.Scenarios |> Array.length = 1
                             ->

                             let items = pr.Filter.Components
                             let lbl = Terms.Components |> getTerm "Componenten"

                             let sel =
                                 if pr.Filter.SelectedComponents |> Array.isEmpty then
                                     items
                                 else
                                     pr.Filter.SelectedComponents

                             items |> Array.map (fun s -> s, s) |> multiSelect lbl sel componentsChange

                         | _ -> null}
                        {match orderContext with
                         | OrderContextView.Settled pr
                         | OrderContextView.Refused(pr, _)
                         | OrderContextView.Changing pr when
                             pr.Filter.Indication.IsSome
                             && pr.Filter.Generic.IsSome
                             && pr.Filter.Route.IsSome
                             ->
                             let sel = pr.Filter.DoseType |> Option.map DoseType.doseTypeToString
                             let items = pr.Filter.DoseTypes
                             let lbl = Terms.``Dose Types`` |> getTerm "Doseer types"

                             items
                             |> Array.map (fun s -> s |> DoseType.doseTypeToString, s |> DoseType.doseTypeToDescription)
                             |> select lbl sel doseTypeChange

                         | _ -> null}
                    </Stack>
                    <Box sx={ {| marginTop = 2 |} }>
                        {resetBar}
                    </Box>
                    {scenarios}
                </Stack>
            </React.Fragment>
            """

        let orderView =
            Order.View
                {|
                    editing = PlanContextPolicy.Editing.Workbench
                    orderContext = dialog |> Option.defaultValue OrderContextView.NoPatient
                    command = orderContextMsg
                    reopen = envOrderContext.ReopenField
                    restoreOrderScenario = envOrderContext.RestoreField
                    closeOrder = handleModalClose
                    busy = (AppEnv.asEnv<AppEnv.IBusy> props.appEnv).Page Global.Pages.Prescribe
                    localizationTerms = localizationTerms
                |}

        let orderDialog = if dialog.IsSome then orderView else null

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';

        <div>
            <Box>
                {notice}
                {refusalNotice}
                {cards}
            </Box>
            {orderDialog}
        </div>
        """
