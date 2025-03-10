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
            prescriptionResult: Deferred<PrescriptionResult>
            updatePrescriptionResult: PrescriptionResult -> unit
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

        let indicationChange s =
            match props.prescriptionResult with
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
                |> props.updatePrescriptionResult
            | _ -> ()

        let medicationChange s =
            match props.prescriptionResult with
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
                |> props.updatePrescriptionResult
            | _ -> ()

        let routeChange s =
            match props.prescriptionResult with
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
                |> props.updatePrescriptionResult
            | _ -> ()

        let diluentChange s =
            match props.prescriptionResult with
            | Resolved pr ->
                { pr with
                    Filter = { pr.Filter with Diluent = s }
                    Scenarios =
                        pr.Scenarios
                        |> Array.map (fun sc ->
                            { sc with
                                Diluent = s
                            }
                        )
                }
                |> props.updatePrescriptionResult
            | _ -> ()

        let doseTypeChange s =
            let dt = s |> Option.map PrescriptionResult.doseTypeFromString
            match props.prescriptionResult with
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
                |> props.updatePrescriptionResult
            | _ -> ()

        let clear () =
            match props.prescriptionResult with
            | Resolved _ ->
                PrescriptionResult.empty |> props.updatePrescriptionResult
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

        let autoComplete isLoading lbl selected dispatch xs =
            Components.Autocomplete.View({|
                updateSelected = dispatch
                label = lbl
                selected = selected
                values = xs
                isLoading = isLoading
            |})

        let progress =
            match props.prescriptionResult with
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

        let typoGraphy (items : Types.TextItem[]) =
            let print item =
                match item with
                | Normal s ->
                    JSX.jsx
                        $"""
                    <Typography color={Mui.Colors.Grey.``700``} display="inline">{s}</Typography>
                    """
                | Bold s ->
                    JSX.jsx
                        $"""
                    <Typography
                    color={Mui.Colors.BlueGrey.``700``}
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
                    display="inline"
                    >
                    <strong> {s} </strong>
                    </Typography>
                    """

            JSX.jsx
                $"""
            import Typography from '@mui/material/Typography';

            <Box display="inline" >
                {items |> Array.map print |> unbox |> React.fragment}
            </Box>
            """

        let displayScenario (pr : PrescriptionResult) med (sc : OrderScenario) =
            if med |> Option.isNone then JSX.jsx $"""<></>"""
            else
                let caption =
                    let renal =
                        sc.RenalRule
                        |> Option.map (fun s ->
                            $" (doseer aanpassing volgens {s})"
                        )
                        |> Option.defaultValue ""
                    $"{sc.Shape}{renal}"

                let onClick (sc : OrderScenario) =
                    { pr with
                        Filter = { pr.Filter with Shape = Some sc.Shape }
                        Scenarios = [| sc |]
                    }
                    |> props.updatePrescriptionResult

                let updateTreatmentPlan () =
                    match props.treatmentPlan with
                    | Resolved tp ->
                        { tp with
                            Scenarios = [| sc |]
                        }
                        |> props.updateTreatmentPlan
                    | _ -> ()

                let item key icon prim sec =
                    let rows =
                        let cells row =
                            row
                            |> Array.mapi (fun i cell ->
                                    JSX.jsx $"""
                                    <TableCell key={i} sx = { {| pt=1; pr=2 |} }>
                                        {cell |> typoGraphy}
                                    </TableCell>
                                    """
                            )

                        let sec =
                             if not isMobile then sec
                             else
                                let add xs =
                                    let plus = [| [| "+ " |> Normal |] |]
                                    xs
                                    |> Array.fold (fun acc x ->
                                        if acc |> Array.isEmpty then x
                                        else
                                            x
                                            |> Array.append plus
                                            |> Array.append acc
                                    ) [||]
                                [|
                                    [|
                                        sec
                                        |> add
                                        |> Array.collect id
                                        //|> Array.collect id
                                    |]
                                |]

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
                        match props.prescriptionResult with
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
                            match props.prescriptionResult with
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
                            match props.prescriptionResult with
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
                            match props.prescriptionResult with
                            | Resolved pr when pr.Filter.Indication.IsSome &&
                                               pr.Filter.Medication.IsSome &&
                                               pr.Filter.Route.IsSome &&
                                               pr.Filter.Diluents |> Array.length > 1 &&
                                               pr.Scenarios |> Array.length = 1 ->

                                    let sc = pr.Scenarios |> Array.head
                                    let sel = sc.Diluent
                                    let items = pr.Filter.Diluents
                                    let lbl = "Verdunningsvorm"
                                    let sel = sel

                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select false lbl sel diluentChange

                            | _ -> JSX.jsx $"<></>"
                        }
                        {
                            match props.prescriptionResult with
                            | Resolved pr when pr.Filter.Indication.IsSome &&
                                               pr.Filter.Medication.IsSome &&
                                               pr.Filter.Route.IsSome ->
                                (false, pr.Filter.DoseType, pr.Filter.DoseTypes)
                                |> fun (isLoading, sel, items) ->
                                    let lbl = "Doseer types"
                                    let sel = sel |> Option.map PrescriptionResult.doseTypeToString

                                    items
                                    |> Array.map (fun s -> s |> PrescriptionResult.doseTypeToString, s |> PrescriptionResult.doseTypeToDescription)
                                    |> select isLoading lbl sel doseTypeChange

                            | _ -> JSX.jsx $"<></>"
                        }

                        <Box sx={ {| mt=2 |} }>
                            <Button variant="text" onClick={clear} fullWidth startIcon={Mui.Icons.Delete} >
                                {Delete |> getTerm "Verwijder"}
                            </Button>
                        </Box>

                    </Stack>
                    <Stack direction="column" >
                        {
                            match props.prescriptionResult with
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
                            prescriptionResult = props.prescriptionResult
                            updatePrescriptionResult = props.updatePrescriptionResult
                            closeOrder = handleModalClose
                            localizationTerms = props.localizationTerms
                        |}
                    }
                </Box>
            </Modal>
        </div>
        """


