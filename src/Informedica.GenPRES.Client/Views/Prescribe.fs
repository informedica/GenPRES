namespace Views


module Prescribe =

    open Fable.Core
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models


    [<JSX.Component>]
    let View (props:
        {|
            orderContext: Deferred<OrderContext>
            updateOrderContext: Api.OrderContextCommand -> unit
            treatmentPlan : Deferred<TreatmentPlan>
            updateTreatmentPlan : TreatmentPlan -> unit
            localizationTerms : Deferred<string [] []>
        |}) =

        let context = React.useContext Global.context
        let lang = context.Localization
        let isMobile = Mui.Hooks.useMediaQuery "(max-width:900px)"

        let getTerm defVal term =
            props.localizationTerms
            |> Deferred.map (fun terms ->
                Localization.getTerm terms lang term
                |> Option.defaultValue defVal
            )
            |> Deferred.defaultValue defVal

        let updateOrderContext = Api.UpdateOrderContext >> props.updateOrderContext

        let indicationChange s =
            match props.orderContext with
            | Resolved pr ->
                if s |> Option.isNone then
                    { pr with
                        Filter =
                            { pr.Filter with
                                Indications = [||]
                                Indication = None
                                DoseTypes = [||]
                                DoseType = None
                            }
                        Scenarios = [||]
                    }
                else
                    { pr with
                        Filter =
                            { pr.Filter with
                                Indication = s
                            }
                    }
                |> updateOrderContext
            | _ -> ()

        let medicationChange s =
            match props.orderContext with
            | Resolved pr ->
                if s |> Option.isNone then
                    { pr with
                        Filter =
                            { pr.Filter with
                                Medications = [||]
                                Medication = None
                                DoseTypes = [||]
                                DoseType = None
                            }
                        Scenarios = [||]
                    }

                else
                    { pr with
                        Filter =
                            { pr.Filter with
                                Medication = s
                            }
                    }
                |> updateOrderContext
            | _ -> ()

        let routeChange s =
            match props.orderContext with
            | Resolved pr ->
                if s |> Option.isNone then
                    { pr with
                        Filter =
                            { pr.Filter with
                                Routes = [||]
                                Route = None
                                DoseTypes = [||]
                                DoseType = None
                            }
                        Scenarios = [||]
                    }
                else
                    { pr with
                        Filter =
                            { pr.Filter with
                                Route = s
                            }
                    }
                |> updateOrderContext
            | _ -> ()

        let formChange s =
            match props.orderContext with
            | Resolved ctx ->
                if s |> Option.isNone then
                    { ctx with
                        Filter =
                            { ctx.Filter with
                                Forms = [||]
                                Form = None
                                DoseTypes = [||]
                                DoseType = None
                            }
                        Scenarios = [||]
                    }
                else
                    { ctx with
                        Filter =
                            { ctx.Filter with
                                Form = s
                            }
                    }
                |> updateOrderContext
            | _ -> ()

        let diluentChange s =
            match props.orderContext with
            | Resolved pr ->
                { pr with
                    Filter = { pr.Filter with Diluent = s }
                }
                |> updateOrderContext
            | _ -> ()

        let componentsChange cs =
            Logging.log "componentsChange" cs
            match props.orderContext with
            | Resolved prctx ->
                { prctx with
                    Filter = { prctx.Filter with SelectedComponents = cs }
                }
                |> updateOrderContext
            | _ -> ()

        let doseTypeChange s =
            let dt = s |> Option.map DoseType.doseTypeFromString
            match props.orderContext with
            | Resolved pr ->
                if dt |> Option.isNone then
                    { pr with
                        Filter =
                            { pr.Filter with
                                DoseTypes = [||]
                                DoseType = None
                            }
                        Scenarios = [||]
                    }
                else
                    { pr with
                        Filter =
                            { pr.Filter with
                                DoseType = dt
                            }
                    }
                |> updateOrderContext
            | _ -> ()

        let clear () =
            match props.orderContext with
            | Resolved _ ->
                OrderContext.empty |> updateOrderContext
            | _ -> ()

        let modalOpen, setModalOpen = React.useState false
        let handleModalClose = fun () -> setModalOpen false

        let select isLoading lbl selected dispatch xs =
            Components.SimpleSelect.View({|
                updateSelected = dispatch
                label = lbl
                selected = selected
                values = xs
                isLoading = isLoading
            |})

        let multiSelect isLoading lbl selected dispatch xs =
            Components.MultipleSelect.View({|
                updateSelected = dispatch
                label = lbl
                selected = selected
                values = xs
                isLoading = isLoading
            |})

        let autoComplete isLoading lbl selected dispatch xs =
            Components.Autocomplete.View({|
                updateSelected = dispatch
                label = lbl
                selected = selected
                values = xs
                isLoading = isLoading
            |})

        let progress =
            match props.orderContext with
            | Resolved _ -> JSX.jsx $"<></>"
            | HasNotStartedYet -> JSX.jsx $"<>Voer eerst patient gegevens in</>"
            | _ ->
                JSX.jsx
                    $"""
                import CircularProgress from '@mui/material/CircularProgress';
                import Backdrop from '@mui/material/Backdrop';

                <Backdrop open={true} sx={ {| color= Mui.Colors.Grey.``200`` |} } >
                    <Box sx={ {| position = "relative"; display= "inline-flex"; p = 20 |} }>
                        <CircularProgress color="primary" size={260} thickness={1} disableShrink={true} />
                        <Box
                            sx={ {|
                            top= 0
                            left= 0
                            bottom= 0
                            right= 0
                            position= "absolute"
                            display= "flex"
                            alignItems= "center"
                            justifyContent= "center"
                            |} }
                        >
                            <Typography
                            variant="subtitle2"
                            component="div"
                            color="white"
                            >... berekenen, even geduld A.U.B.</Typography>
                        </Box>
                    </Box>
                </Backdrop>
                """


        let displayScenario (pr : OrderContext) med (sc : OrderScenario) =
            if med |> Option.isNone then JSX.jsx $"""<></>"""
            else
                let caption =
                    let renal =
                        sc.RenalRule
                        |> Option.map (fun s ->
                            $" (doseer aanpassing volgens {s})"
                        )
                        |> Option.defaultValue ""
                    $"{sc.Form}{renal}"

                let onClick (sc : OrderScenario) =
                    { pr with
                        Filter = { pr.Filter with Form = Some sc.Form }
                        Scenarios = [| sc |]
                    }
                    |> Api.SelectOrderScenario
                    |> props.updateOrderContext

                let updateTreatmentPlan () =
                    match props.treatmentPlan with
                    | Resolved tp ->
                        { tp with
                            Scenarios =
                                [| sc |]
                                |> Array.append tp.Scenarios
                        }
                        |> props.updateTreatmentPlan
                    | _ -> ()

                let item key icon prim (sec : TextBlock [][]) =
                    let rows =
                        let cells row =
                            row
                            |> Array.mapi (fun i cell ->
                                    JSX.jsx $"""
                                    <TableCell key={i} sx = { {| pt=1; pr=2 |} }>
                                        {cell |> Mui.TypoGraphy.fromTextBlock}
                                    </TableCell>
                                    """
                            )

                        let getItems tb =
                            match tb with
                            | Valid itms
                            | Caution itms
                            | Warning itms
                            | Alert itms ->
                                itms
                                |> Array.append [| " " |> Normal |]

                        // get the max alert level
                        let maxTb (xs: TextBlock [][]) =
                            if xs |> Array.isEmpty then Valid
                            else
                                xs
                                |> Array.collect (fun tbs ->
                                    if tbs |> Array.isEmpty then [| 0 |]
                                    else
                                        tbs
                                        |> Array.map (fun tb ->
                                            match tb with
                                            | Valid _ -> 0
                                            | Caution _ -> 1
                                            | Warning _ -> 2
                                            | Alert _ -> 3
                                        )
                                )
                                |> Array.max
                                |> function
                                | 0 -> Valid
                                | 1 -> Caution
                                | 2 -> Warning
                                | 3 -> Alert
                                | i -> failwith $"not a valid textblock: {i}"

                        let sec =
                             if not isMobile then sec
                             else
                                // flatten the TextBlock [] [] to a single TextBlock
                                let add xs =
                                    let plus = [| [| " + " |> Normal |] |]

                                    xs
                                    |> Array.fold (fun acc x ->
                                        if acc |> Array.isEmpty then x
                                        else
                                            x
                                            |> Array.append plus
                                            |> Array.append acc
                                    ) [||]
                                    |> Array.collect id

                                sec
                                |> Array.map (Array.map getItems)
                                |> add
                                |> (sec |> maxTb)
                                |> Array.singleton
                                |> Array.singleton

                        sec
                        |> Array.mapi (fun i row ->
                            JSX.jsx $"""
                                <TableRow key={i} sx={ {| border=0 |} } >
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
                        <TableContainer sx={ {| width="max-content" |} } >
                            <Table padding="none" size="small" >
                                <TableBody>
                                    <TableRow sx={ {| border=0; ``& td``={| borderBottom=0 |} |} } >
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
                        <Typography variant="h6" >
                            {caption}
                        </Typography>
                        <List sx={ {| width="100%"; maxWidth=1200; bgcolor=Mui.Colors.Grey.``50`` |} }>
                            {
                                [|
                                    item "prescription" Mui.Icons.Notes (Terms.``Prescribe Prescription`` |> getTerm "Voorschrift") sc.Prescription
                                    if sc.Preparation |> Array.length > 0 then
                                        item "preparation" Mui.Icons.Vaccines (Terms.``Prescribe Preparation`` |> getTerm "Bereiding") sc.Preparation
                                    item "administration" Mui.Icons.MedicationLiquid (Terms.``Prescribe Administration`` |> getTerm "Toediening") sc.Administration
                                |]
                                |> unbox
                                |> React.fragment
                            }
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

                <Box sx={ {| height="100%" |} } >
                    <Card sx={ {| padding=0; minWidth = 275 |}  }>
                        <CardContent sx={ {| padding=0 |} }>
                            {content}
                            {progress}
                        </CardContent>
                        <CardActions>
                            <Button
                                size="small"
                                onClick={fun () -> setModalOpen true; onClick sc}
                                startIcon={Mui.Icons.CalculateIcon}
                            >{Edit |> getTerm "bewerken"}</Button>
                            <Button
                                size="small"
                                onClick={updateTreatmentPlan}
                                startIcon={Mui.Icons.Add}
                            >Voorschrijven</Button>
                        </CardActions>
                    </Card>
                </Box>
                """

        let stackDirection =
            if isMobile then "column" else "row"

        let cards =
            JSX.jsx
                $"""
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';

            <React.Fragment>
                <Stack direction="column" spacing={3}>
                    <Typography sx={ {| fontSize=14 |} } color="text.secondary" >
                        {Terms.``Prescribe Scenarios`` |> getTerm "Medicatie scenario's"}
                    </Typography>
                    {
                        match props.orderContext with
                        | Resolved pr -> false, pr.Filter.Indication, pr.Filter.Indications
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            let lbl = Terms.``Prescribe Indications`` |> getTerm "Indicaties"

                            if isMobile then
                                items
                                |> Array.map (fun s -> s, s)
                                |> select isLoading lbl sel indicationChange
                            else
                                items
                                |> autoComplete isLoading lbl sel indicationChange
                    }
                    <Stack direction={stackDirection} spacing={3} >
                        {
                            match props.orderContext with
                            | Resolved pr -> false, pr.Filter.Medication, pr.Filter.Medications
                            | _ -> true, None, [||]
                            |> fun (isLoading, sel, items) ->
                                let lbl = Terms.``Prescribe Medications`` |> getTerm "Medicatie"

                                if isMobile then
                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select isLoading lbl sel medicationChange
                                else
                                    items
                                    |> autoComplete isLoading lbl sel medicationChange

                        }
                        {
                            match props.orderContext with
                            | Resolved pr -> false, pr.Filter.Route, pr.Filter.Routes
                            | _ -> true, None, [||]
                            |> fun (isLoading, sel, items) ->
                                let lbl = Terms.``Prescribe Routes`` |> getTerm "Routes"

                                if isMobile then
                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select isLoading lbl sel routeChange
                                else
                                    items
                                    |> autoComplete isLoading lbl sel routeChange

                        }
                        {
                            match props.orderContext with
                            | Resolved ctx when ctx.Filter.Forms |> Array.length >= 1 &&
                                                (not isMobile || ctx.Scenarios |> Array.length <> 1) ->
                                false, ctx.Filter.Form, ctx.Filter.Forms
                            | Resolved _ -> false, None, [||]
                            | _ -> true, None, [||]
                            |> fun (isLoading, sel, items) ->
                                let lbl = "Vorm"

                                if items |> Array.isEmpty then JSX.jsx $"<></>"
                                else
                                    if isMobile then
                                        items
                                        |> Array.map (fun s -> s, s)
                                        |> select isLoading lbl sel formChange
                                    else
                                        items
                                        |> Array.map (fun s -> s, s)
                                        |> select isLoading lbl sel formChange
                        }
                        {
                            match props.orderContext with
                            | Resolved pr when pr.Filter.Indication.IsSome &&
                                               pr.Filter.Medication.IsSome &&
                                               pr.Filter.Route.IsSome &&
                                               pr.Filter.Diluents |> Array.length > 1 &&
                                               pr.Scenarios |> Array.length = 1 ->

                                    let sel = pr.Filter.Diluent
                                    let items = pr.Filter.Diluents
                                    let lbl = "Verdunningsvorm"
                                    let sel = sel

                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select false lbl sel diluentChange

                            | _ -> JSX.jsx $"<></>"
                        }
                        {
                            match props.orderContext with
                            | Resolved pr when pr.Filter.Indication.IsSome &&
                                               pr.Filter.Medication.IsSome &&
                                               pr.Filter.Route.IsSome &&
                                               pr.Filter.Components |> Array.length > 1 &&
                                               pr.Scenarios |> Array.length = 1 ->

                                    let items = pr.Filter.Components
                                    let lbl = "Componenten"
                                    let sel =
                                        if pr.Filter.SelectedComponents |> Array.isEmpty then items
                                        else pr.Filter.SelectedComponents

                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> multiSelect false lbl sel componentsChange

                            | _ -> JSX.jsx $"<></>"
                        }
                        {
                            match props.orderContext with
                            | Resolved pr when pr.Filter.Indication.IsSome &&
                                               pr.Filter.Medication.IsSome &&
                                               pr.Filter.Route.IsSome ->
                                (false, pr.Filter.DoseType, pr.Filter.DoseTypes)
                                |> fun (isLoading, sel, items) ->
                                    let lbl = "Doseer types"
                                    let sel = sel |> Option.map DoseType.doseTypeToString

                                    items
                                    |> Array.map (fun s -> s |> DoseType.doseTypeToString, s |> DoseType.doseTypeToDescription)
                                    |> select isLoading lbl sel doseTypeChange

                            | _ -> JSX.jsx $"<></>"
                        }
                    </Stack>
                    <Box sx={ {| mt=2 |} }>
                        <Button variant="text" onClick={clear} fullWidth startIcon={Mui.Icons.Delete} >
                            {Delete |> getTerm "Verwijder"}
                        </Button>
                    </Box>
                    <Stack direction="column" >
                        {
                            match props.orderContext with
                            | Resolved pr ->
                                pr.Scenarios
                                |> Array.map (displayScenario pr pr.Filter.Medication)
                                |> unbox
                                |> React.fragment
                            | _ -> Seq.empty |> React.fragment
                        }
                    </Stack>
                </Stack>
            </React.Fragment>
            """

        let modalStyle =
            {|
                position="absolute"
                top= "50%"
                left= "50%"
                transform= "translate(-50%, -50%)"
                width= 400
                bgcolor= "background.paper"
                boxShadow= 24
                borderRadius = "16px"
            |}

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Modal from '@mui/material/Modal';

        <div>
            <Box sx={ {| height="100%" |} }>
                {cards}
                {progress}
            </Box>
            <Modal open={modalOpen} onClose={handleModalClose} >
                <Box sx={modalStyle}>
                    {
                        Order.View {|
                            orderContext = props.orderContext
                            updateOrderScenario = Api.UpdateOrderScenario >> props.updateOrderContext
                            refreshOrderScenario = Api.ResetOrderScenario >> props.updateOrderContext
                            closeOrder = handleModalClose
                            localizationTerms = props.localizationTerms
                        |}
                    }
                </Box>
            </Modal>
        </div>
        """