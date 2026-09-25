namespace Views


module Interactions =


    open Fable.Core
    open Fable.React
    open Feliz
    open Elmish
    open Shared
    open Shared.Types
    open OrderPlanMachine


    module private Elmish =


        // the drugs of the plan as settled; none while it changes, as before
        let getPlanDrugs (orderPlan: OrderPlanView) =
            match orderPlan with
            | OrderPlanView.Settled(tp, _) ->
                Shared.Models.OrderPlan.orders tp
                |> Array.map _.Name
                |> Array.distinct
                |> Array.toList
            | OrderPlanView.NoPatient
            | OrderPlanView.Changing _ -> []


        let getCombinedDrugs planDrugs manualDrugs = (planDrugs @ manualDrugs) |> List.distinct


        type State =
            {
                DrugInput: string
                ManualDrugs: string list
            }


        type Msg =
            | UpdateDrugInput of string
            | AddDrug of string
            | RemoveDrug of string
            | ClearManualDrugs


        let init () =
            {
                DrugInput = ""
                ManualDrugs = []
            },
            Cmd.none


        let update
            (orderPlan: OrderPlanView)
            (checkInteractions: string list -> unit)
            (msg: Msg)
            (state: State)
            : State * Cmd<Msg>
            =
            let triggerCheck manualDrugs =
                let combined = getCombinedDrugs (getPlanDrugs orderPlan) manualDrugs

                if combined.Length >= 2 then
                    checkInteractions combined
                else
                    checkInteractions []

            match msg with
            | UpdateDrugInput s -> { state with DrugInput = s }, Cmd.none

            | AddDrug drug ->
                let drug = drug.Trim()
                let planDrugs = getPlanDrugs orderPlan

                if
                    drug = ""
                    || state.ManualDrugs |> List.contains drug
                    || planDrugs |> List.contains drug
                then
                    { state with DrugInput = "" }, Cmd.none
                else
                    let newState =
                        { state with
                            DrugInput = ""
                            ManualDrugs = state.ManualDrugs @ [ drug ]
                        }

                    triggerCheck newState.ManualDrugs
                    newState, Cmd.none

            | RemoveDrug drug ->
                let newState = { state with ManualDrugs = state.ManualDrugs |> List.filter (fun d -> d <> drug) }

                triggerCheck newState.ManualDrugs
                newState, Cmd.none

            | ClearManualDrugs ->
                let newState =
                    { state with
                        ManualDrugs = []
                        DrugInput = ""
                    }

                triggerCheck newState.ManualDrugs
                newState, Cmd.none


    open Elmish


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envInteractions = AppEnv.asEnv<AppEnv.IInteractions> props.appEnv
        let interactions = envInteractions.Interactions
        let interactionDrugNames = envInteractions.InteractionDrugNames
        let checkInteractions = envInteractions.CheckInteractions
        let orderPlan = (AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv).OrderPlan

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let state, dispatch = React.useElmish (init (), update orderPlan checkInteractions, [| box orderPlan |])

        let planDrugs = getPlanDrugs orderPlan

        // Re-check interactions when plan drugs change (e.g., order added or removed).
        // Skip while a check runs to avoid duplicate API calls (processApiMsg also
        // dispatches CheckInteractions on OrderPlan API responses).
        let planDrugsKey = planDrugs |> String.concat "|"

        React.useEffect (
            (fun () ->
                match interactions with
                | InProgress
                | Refreshing _ -> ()
                | _ ->
                    let combined = getCombinedDrugs planDrugs state.ManualDrugs

                    if combined.Length >= 2 then
                        checkInteractions combined
                    else
                        checkInteractions []
            ),
            [| box planDrugsKey |]
        )

        // the rows shown stay while the drugs are checked again; the progress shows over them
        let interactionRows =
            match interactions with
            | Resolved interactions
            | Refreshing interactions -> interactions
            | _ -> [||]

        let isLoading =
            match interactions with
            | InProgress
            | Refreshing _ -> true
            | _ -> false

        let hasChecked =
            match interactions with
            | Resolved _
            | Refreshing _ -> true
            | _ -> false

        let drugNameValues =
            match interactionDrugNames with
            | Resolved names
            | Refreshing names -> names
            | _ -> [||]

        let isDrugNamesLoading =
            match interactionDrugNames with
            | InProgress
            | Refreshing _ -> true
            | _ -> false

        let planChips =
            planDrugs
            |> List.toArray
            |> Array.map (fun drug ->
                JSX.jsx
                    $"""
                <Chip
                    key={drug}
                    label={drug}
                    color="primary"
                    variant="outlined"
                />
                """
            )

        let manualChips =
            state.ManualDrugs
            |> List.filter (fun d -> planDrugs |> List.contains d |> not)
            |> List.toArray
            |> Array.map (fun drug ->
                let onDelete = fun _ -> RemoveDrug drug |> dispatch

                JSX.jsx
                    $"""
                <Chip
                    key={drug}
                    label={drug}
                    onDelete={onDelete}
                />
                """
            )

        let chipList = Array.append planChips manualChips

        let drug1Label = Terms.``Interactions Drug 1`` |> getTerm "Medicatie 1"
        let drug2Label = Terms.``Interactions Drug 2`` |> getTerm "Medicatie 2"
        let classLabel = Terms.``Interactions Class`` |> getTerm "Interactie klasse"

        let noneFoundLabel = Terms.``Interactions None Found`` |> getTerm "Geen interacties gevonden"

        let interactionTable =
            if interactionRows.Length > 0 then
                let rows =
                    interactionRows
                    |> Array.mapi (fun i interaction ->
                        let cls1, cls2 = interaction.Name
                        let classText = $"%s{cls1} / %s{cls2}"

                        JSX.jsx
                            $"""
                        <TableRow key={i}>
                            <TableCell>{interaction.Drug1}</TableCell>
                            <TableCell>{interaction.Drug2}</TableCell>
                            <TableCell>{classText}</TableCell>
                        </TableRow>
                        """
                    )

                JSX.jsx
                    $"""
                <TableContainer>
                    <Table size="small">
                        <TableHead>
                            <TableRow>
                                <TableCell>{drug1Label}</TableCell>
                                <TableCell>{drug2Label}</TableCell>
                                <TableCell>{classLabel}</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {rows}
                        </TableBody>
                    </Table>
                </TableContainer>
                """
            elif hasChecked && not isLoading then
                Components.Notice.View
                    {|
                        kind = Components.Notice.Kind.Empty
                        title = None
                        message = noneFoundLabel
                        action = None
                        onClose = None
                    |}
            else
                null

        let loadingIndicator =
            if isLoading then
                JSX.jsx $"""<CircularProgress />"""
            else
                null

        let titleLabel = Terms.``Interactions`` |> getTerm "Interacties"

        let autocomplete =
            Components.Autocomplete.View
                {|
                    label = Terms.``Interactions Medication`` |> getTerm "Medicatie"
                    selected = None
                    values = drugNameValues
                    updateSelected =
                        fun opt ->
                            match opt with
                            | Some drug -> AddDrug drug |> dispatch
                            | None -> ()
                    isLoading = isDrugNamesLoading
                    disabled = false
                    // the box holds no choice, but it does hold what is being typed, and the
                    // cross is how that is thrown away in one go
                    canClear = true
                |}

        let deleteLabel = Terms.``Delete`` |> getTerm "Verwijder"
        let onClickClear = fun _ -> ClearManualDrugs |> dispatch

        let sxTitle = {| fontSize = 14 |}

        let sxChips =
            {|
                flexWrap = "wrap"
                gap = 1
            |}

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Card from '@mui/material/Card';
        import CardContent from '@mui/material/CardContent';
        import Typography from '@mui/material/Typography';
        import Stack from '@mui/material/Stack';
        import Button from '@mui/material/Button';
        import Chip from '@mui/material/Chip';
        import Table from '@mui/material/Table';
        import TableBody from '@mui/material/TableBody';
        import TableCell from '@mui/material/TableCell';
        import TableContainer from '@mui/material/TableContainer';
        import TableHead from '@mui/material/TableHead';
        import TableRow from '@mui/material/TableRow';
        import CircularProgress from '@mui/material/CircularProgress';
        import Divider from '@mui/material/Divider';

        <Box>
            <Card>
                <CardContent>
                    <Stack direction="column" spacing={3}>

                        <Typography sx={sxTitle} color="text.secondary" gutterBottom>
                            {titleLabel}
                        </Typography>

                        <Stack direction="row" spacing={1} sx={ {| alignItems = "center" |} }>
                            {autocomplete}
                        </Stack>

                        <Stack direction="row" spacing={1} sx={sxChips}>
                            {chipList}
                        </Stack>

                        <Stack direction="row" spacing={2}>
                            <Button
                                variant="text"
                                onClick={onClickClear}
                                disabled={state.ManualDrugs.IsEmpty}
                                startIcon={Mui.Icons.Delete}
                            >
                                {deleteLabel}
                            </Button>
                        </Stack>

                        {loadingIndicator}

                        <Divider />

                        {interactionTable}
                    </Stack>
                </CardContent>
            </Card>
        </Box>
        """
