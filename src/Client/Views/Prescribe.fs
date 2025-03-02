namespace Views

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


module Prescribe =

    open Shared


    [<JSX.Component>]
    let View (props:
        {|
            scenarioResult: Deferred<Types.ScenarioResult>
            updateScenarioResult: Types.ScenarioResult -> unit
            selectScenario : (Types.Scenario * Types.Order option) -> unit
            order: Deferred<LoadedOrder option>
            loadOrder: OrderLoader -> unit
            addOrderToPlan : (Scenario * Order) -> unit
            updateScenarioOrder : unit -> unit
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
            match props.scenarioResult with
            | Resolved sr ->
                if s |> Option.isNone then
                    { sr with
                        Filter =
                            { sr.Filter with
                                Indications = [||]
                                Indication = None
                                Diluents = [||]
                                Diluent = None
                                DoseTypes = [||]
                                DoseType = None
                            }
                        Scenarios = [||]
                    }
                else
                    { sr with
                        Filter =
                            { sr.Filter with
                                Indication = s
                            }
                    }
                |> props.updateScenarioResult
            | _ -> ()

        let medicationChange s =
            match props.scenarioResult with
            | Resolved sr ->
                if s |> Option.isNone then
                    { sr with
                        Filter =
                            { sr.Filter with
                                Medications = [||]
                                Medication = None
                                Diluents = [||]
                                Diluent = None
                                DoseTypes = [||]
                                DoseType = None
                            }
                        Scenarios = [||]
                    }

                else
                    { sr with
                        Filter =
                            { sr.Filter with
                                Medication = s
                            }
                    }
                |> props.updateScenarioResult
            | _ -> ()

        let routeChange s =
            match props.scenarioResult with
            | Resolved sr ->
                if s |> Option.isNone then
                    { sr with
                        Filter =
                            { sr.Filter with
                                Routes = [||]
                                Route = None
                                Diluents = [||]
                                Diluent = None
                                DoseTypes = [||]
                                DoseType = None
                            }
                        Scenarios = [||]
                    }
                else
                    { sr with
                        Filter =
                            { sr.Filter with
                                Route = s
                            }
                    }
                |> props.updateScenarioResult
            | _ -> ()

        let diluentChange s =
            match props.scenarioResult with
            | Resolved sr ->
                if s |> Option.isNone then
                    { sr with
                        Filter =
                            { sr.Filter with
                                Diluents = [||]
                                Diluent = None
                                DoseTypes = [||]
                                DoseType = None
                            }
                        Scenarios = [||]
                    }
                else
                    { sr with
                        Filter =
                            { sr.Filter with
                                Diluent = s
                            }
                    }
                |> props.updateScenarioResult
            | _ -> ()

        let doseTypeChange s =
            let dt = s |> Option.map ScenarioResult.doseTypeFromString
            match props.scenarioResult with
            | Resolved sr ->
                if dt |> Option.isNone then
                    { sr with
                        Filter =
                            { sr.Filter with

                                DoseTypes = [||]
                                DoseType = None
                            }
                        Scenarios = [||]
                    }
                else
                    { sr with
                        Filter =
                            { sr.Filter with
                                DoseType = dt
                            }
                    }
                |> props.updateScenarioResult
            | _ -> ()

        let clear () =
            match props.scenarioResult with
            | Resolved _ ->
                ScenarioResult.empty |> props.updateScenarioResult
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
            match props.scenarioResult with
            | Resolved _ -> JSX.jsx $"<></>"
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

        let displayScenario med (sc : Types.Scenario) =
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

                let ord = sc.Order

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
                                onClick={fun () -> setModalOpen true; (sc, ord) |> props.selectScenario}
                                startIcon={Mui.Icons.CalculateIcon}
                            >{Terms.Edit |> getTerm "bewerken"}</Button>
                            <Button
                                size="small"
                                onClick={fun () -> if ord.IsSome then (sc, ord.Value) |> props.addOrderToPlan }
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
                        match props.scenarioResult with
                        | Resolved sr -> false, sr.Filter.Indication, sr.Filter.Indications
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            let lbl = (Terms.``Prescribe Indications`` |> getTerm "Indicaties")

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
                            match props.scenarioResult with
                            | Resolved sr -> false, sr.Filter.Medication, sr.Filter.Medications
                            | _ -> true, None, [||]
                            |> fun (isLoading, sel, items) ->
                                let lbl = (Terms.``Prescribe Medications`` |> getTerm "Medicatie")

                                if isMobile then
                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select isLoading lbl sel medicationChange
                                else
                                    items
                                    |> autoComplete isLoading lbl sel medicationChange

                        }
                        {
                            match props.scenarioResult with
                            | Resolved sr -> false, sr.Filter.Route, sr.Filter.Routes
                            | _ -> true, None, [||]
                            |> fun (isLoading, sel, items) ->
                                let lbl = (Terms.``Prescribe Routes`` |> getTerm "Routes")

                                if isMobile then
                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select isLoading lbl sel routeChange
                                else
                                    items
                                    |> autoComplete isLoading lbl sel routeChange

                        }
                        {
                            match props.scenarioResult with
                            | Resolved sr when sr.Filter.Indication.IsSome &&
                                               sr.Filter.Medication.IsSome &&
                                               sr.Filter.Route.IsSome &&
                                               sr.Filter.Diluents |> Array.length > 1 ->
                                (false, sr.Filter.Diluent, sr.Filter.Diluents)
                                |> fun (isLoading, sel, items) ->
                                    let lbl = "Verdunningsvorm"
                                    let sel = sel

                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select isLoading lbl sel diluentChange

                            | _ -> JSX.jsx $"<></>"
                        }
                        {
                            match props.scenarioResult with
                            | Resolved sr when sr.Filter.Indication.IsSome &&
                                               sr.Filter.Medication.IsSome &&
                                               sr.Filter.Route.IsSome ->
                                (false, sr.Filter.DoseType, sr.Filter.DoseTypes)
                                |> fun (isLoading, sel, items) ->
                                    let lbl = "Doseer types"
                                    let sel = sel |> Option.map ScenarioResult.doseTypeToString

                                    items
                                    |> Array.map (fun s -> s |> ScenarioResult.doseTypeToString, s |> ScenarioResult.doseTypeToDescription)
                                    |> select isLoading lbl sel doseTypeChange

                            | _ -> JSX.jsx $"<></>"
                        }

                        <Box sx={ {| mt=2 |} }>
                            <Button variant="text" onClick={clear} fullWidth startIcon={Mui.Icons.Delete} >
                                {Terms.Delete |> getTerm "Verwijder"}
                            </Button>
                        </Box>

                    </Stack>
                    <Stack direction="column" >
                        {
                            match props.scenarioResult with
                            | Resolved sr ->
                                sr.Filter.Medication,
                                sr.Scenarios
                            | _ -> None, [||]
                            |> fun (med, scs) ->
                                scs
                                |> Array.map (displayScenario med)
                            |> unbox |> React.fragment
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
                            order = props.order
                            loadOrder = props.loadOrder
                            updateScenarioOrder = props.updateScenarioOrder
                            closeOrder = handleModalClose
                            localizationTerms = props.localizationTerms
                        |}
                    }
                </Box>
            </Modal>
        </div>
        """


