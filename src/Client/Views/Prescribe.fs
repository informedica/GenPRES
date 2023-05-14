namespace Views

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


module Prescribe =


    module private Elmish =

        open Feliz
        open Feliz.UseElmish
        open Elmish
        open Shared
        open Utils
        open FSharp.Core


        type State =
            {
                Dialog: string list
                Indication: string option
                Medication: string option
                Route: string option
            }


        type Msg =
            | RowClick of int * string list
            | CloseDialog
            | IndicationChange of string option
            | MedicationChange of string option
            | RouteChange of string option


        let empty =
            {
                Dialog = []
                Indication = None
                Medication = None
                Route = None
            }


        let init ord (scenarios: Deferred<ScenarioResult>) =
            let state =
                match scenarios with
                | Resolved sc ->
                    {
                        Dialog = []
                        Indication = sc.Indication
                        Medication = sc.Medication
                        Route = sc.Route
                    }
                | _ -> empty
            state, Cmd.none


        let update
            (scenarios: Deferred<ScenarioResult>)
            updateScenario
            (msg: Msg)
            state
            =
            let clear (sc : ScenarioResult) =
                { sc with
                    Indication = None
                    Medication = None
                    Route = None
                }

            match msg with
            | RowClick (i, xs) ->
                Logging.log "rowclick:" i
                { state with Dialog = xs }, Cmd.none

            | CloseDialog -> { state with Dialog = [] }, Cmd.none

            | IndicationChange s ->
                printfn $"indication change {s}"
                match scenarios with
                | Resolved sc ->
                    if s |> Option.isNone then ScenarioResult.empty
                    else
                        { sc with Indication = s }
                    |> updateScenario
                | _ -> ()

                { state with Indication = s }, Cmd.none

            | MedicationChange s ->
                match scenarios with
                | Resolved sc ->
                    if s |> Option.isNone then ScenarioResult.empty
                    else
                        { sc with Medication = s }
                    |> updateScenario
                | _ -> ()

                { state with Medication = s }, Cmd.none

            | RouteChange s ->
                match scenarios with
                | Resolved sc ->
                    if s |> Option.isNone then ScenarioResult.empty
                    else
                        { sc with Route = s }
                    |> updateScenario
                | _ -> ()

                { state with Route = s }, Cmd.none


    open Elmish
    open Shared


    [<JSX.Component>]
    let View (props:
        {|
            scenarios: Deferred<Types.ScenarioResult>
            updateScenario: Types.ScenarioResult -> unit
            selectOrder : (Types.Scenario * Shared.Types.Order option) -> unit
            order : Deferred<Types.Order option>
            loadOrder : Types.Order -> unit
            updateScenarioOrder : unit -> unit
            localizationTerms : Deferred<string [] []>
        |}) =

        let lang = React.useContext(Global.languageContext)

        let getTerm defVal term =
            props.localizationTerms
            |> Deferred.map (fun terms ->
                Localization.getTerm terms lang term
                |> Option.defaultValue defVal
            )
            |> Deferred.defaultValue defVal

        let state, dispatch =
            React.useElmish (
                init props.order props.scenarios,
                update props.scenarios props.updateScenario,
                [| box props.order; box props.scenarios; box props.updateScenario; box props.selectOrder |]
            )

        let modalOpen, setModalOpen = React.useState(false)

        let handleModalClose = fun () -> setModalOpen false

        let select isLoading lbl selected dispatch xs =
            Components.SimpleSelect.View({|
                updateSelected = dispatch
                label = lbl
                selected = selected
                values = xs
                isLoading = isLoading
            |})

        let progress =
            match props.scenarios with
            | Resolved _ -> JSX.jsx $"<></>"
            | _ ->
                JSX.jsx
                    $"""
                import CircularProgress from '@mui/material/CircularProgress';

                <Box sx={ {| mt = 5; display = "flex"; p = 20 |} }>
                    <CircularProgress />
                </Box>
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
                let med =
                    med |> Option.defaultValue ""
                    |> fun s -> $"{s} {sc.Shape} {sc.DoseType}"

                let ord =
                    sc.Order

                let item icon prim sec =
                    JSX.jsx
                        $"""
                    <ListItem>
                        <ListItemIcon>
                            {icon}
                        </ListItemIcon>
                        <Box sx={ {| display="flex"; flexDirection="column" |} }>
                            {prim}
                            {sec |> typoGraphy}
                        </Box>
                    </ListItem>
                    """

                let content =
                    JSX.jsx
                        $"""
                    <React.Fragment>
                        <Typography variant="h6">
                            {med}
                        </Typography>
                        <List sx={ {| width="100%"; maxWidth= 800; bgcolor = "background.paper" |} }>
                            {
                                [|
                                    item Mui.Icons.Notes (Terms.``Prescribe Prescription`` |> getTerm "Voorschrift") sc.Prescription
                                    item Mui.Icons.Vaccines (Terms.``Prescribe Preparation`` |> getTerm "Bereiding") sc.Preparation
                                    item Mui.Icons.MedicationLiquid (Terms.``Prescribe Administration`` |> getTerm "Toediening") sc.Administration
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

                <Box sx={ {| mt=3; height="100%" |} } >
                    <Card sx={ {| minWidth = 275 |}  }>
                        <CardContent>
                            {content}
                            {progress}
                        </CardContent>
                        <CardActions>
                            <Button
                                size="small"
                                onClick={fun () -> setModalOpen true; (sc, ord) |> props.selectOrder}
                            >{Terms.Edit |> getTerm "bewerken"}</Button>
                        </CardActions>
                    </Card>
                </Box>
                """

        let stackDirection =
            if  Mui.Hooks.useMediaQuery "(max-width:900px)" then "column" else "row"

        let cards =
            JSX.jsx
                $"""
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';

            <React.Fragment>
                <Typography sx={ {| fontSize=14 |} } color="text.secondary" gutterBottom>
                    {Terms.``Prescribe Scenarios`` |> getTerm "Medicatie scenario's"}
                </Typography>
                <Stack direction={stackDirection} spacing={3} >

                    {
                        match props.scenarios with
                        | Resolved scrs -> false, scrs.Indication, scrs.Indications
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading (Terms.``Prescribe Indications`` |> getTerm "Indicaties") sel (IndicationChange >> dispatch)
                    }
                    {
                        match props.scenarios with
                        | Resolved scrs -> false, scrs.Medication, scrs.Medications
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading (Terms.``Prescribe Medications`` |> getTerm "Medicatie") sel (MedicationChange >> dispatch)
                    }
                    {
                        match props.scenarios with
                        | Resolved scrs -> false, scrs.Route, scrs.Routes
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading (Terms.``Prescribe Routes`` |> getTerm "Routes") sel (RouteChange >> dispatch)
                    }
                </Stack>
                <Stack direction="column" sx={ {| mt = 1 |} } >
                    {
                        match props.scenarios with
                        | Resolved sc ->
                            sc.Medication,
                            sc.Scenarios
                        | _ -> None, [||]
                        |> fun (med, scs) ->
                            scs
                            |> Array.map (displayScenario med)
                        |> unbox |> React.fragment
                    }
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


