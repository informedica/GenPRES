namespace Views


module Formulary =

    open Fable.Core
    open Fable.React
    open Feliz
    open Shared
    open Shared.Models
    open Shared.Types
    open Elmish
    open OrderContextMachine


    module private Elmish =


        type State =
            {
                Generic: string option
                Indication: string option
                Route: string option
                Form: string option
                DoseType: string option
            }


        type Msg =
            | GenericChange of string option
            | IndicationChange of string option
            | RouteChange of string option
            | FormChange of string option
            | DoseTypeChange of string option
            | Clear


        let empty =
            {
                Generic = None
                Indication = None
                Route = None
                Form = None
                DoseType = None
            }


        let init (form: Deferred<Formulary>) =
            let state =
                match form with
                | Resolved form ->
                    {
                        Generic = form.Generic //|> Option.orElse gen
                        Indication = form.Indication //|> Option.orElse ind
                        Route = form.Route //|> Option.orElse rte
                        Form = form.Form
                        DoseType = form.DoseType |> Option.map DoseType.doseTypeToDescription
                    }
                | _ -> empty

            state, Cmd.none


        let update (formulary: Deferred<Formulary>) updateFormulary (msg: Msg) (state: State) : State * Cmd<Msg> =
            let clear (form: Formulary) =
                { form with
                    Indications = [||]
                    Indication = None
                    Generics = [||]
                    Generic = None
                    Routes = [||]
                    Route = None
                    Forms = [||]
                    Form = None
                    DoseTypes = [||]
                    DoseType = None
                    PatientCategories = [||]
                    PatientCategory = None
                }

            match msg with
            | Clear ->
                match formulary with
                | Resolved form -> form |> clear |> updateFormulary
                | _ -> ()

                { state with
                    Indication = None
                    Generic = None
                    Route = None
                    DoseType = None
                },
                Cmd.none

            | IndicationChange s ->
                match formulary with
                | Resolved form ->
                    if s |> Option.isNone then
                        { form with
                            Indication = None
                            Indications = [||]
                        }
                    else
                        { form with Indication = s }
                    |> updateFormulary
                | _ -> ()

                { state with Indication = s }, Cmd.none

            | GenericChange s ->
                match formulary with
                | Resolved form ->
                    if s |> Option.isNone then
                        { form with
                            Generic = None
                            Generics = [||]
                            Indication = None
                            Indications = [||]
                            Form = None
                            Forms = [||]
                            DoseType = None
                            DoseTypes = [||]
                        }
                    else
                        { form with Generic = s }
                    |> updateFormulary
                | _ -> ()

                { state with Generic = s }, Cmd.none

            | RouteChange s ->
                match formulary with
                | Resolved form ->
                    { form with
                        DoseType = None
                        DoseTypes = [||]
                        Forms = [||]
                        Form = None
                        Route = s
                    }
                    |> updateFormulary
                | _ -> ()

                { state with Route = s }, Cmd.none

            | FormChange s ->
                match formulary with
                | Resolved form ->
                    { form with
                        DoseType = None
                        DoseTypes = [||]
                        Form = s
                    }
                    |> updateFormulary
                | _ -> ()

                { state with Form = s }, Cmd.none

            | DoseTypeChange s ->
                match formulary with
                | Resolved form ->
                    { form with DoseType = s |> Option.map DoseType.doseTypeFromString }
                    |> updateFormulary
                | _ -> ()

                { state with DoseType = s }, Cmd.none

    open Elmish


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envFormulary = AppEnv.asEnv<AppEnv.IFormulary> props.appEnv
        let formulary = envFormulary.Formulary
        let updateFormulary = envFormulary.UpdateFormulary

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let state, dispatch =
            React.useElmish (init formulary, update formulary updateFormulary, [| box formulary |])

        // the filter is the workbench's: a change here is evaluated there, so the selects are
        // greyed while a workbench request is under way
        let busy =
            match (AppEnv.asEnv<AppEnv.IOrderContext> props.appEnv).OrderContext with
            | OrderContextView.Evaluating
            | OrderContextView.Changing _ -> true
            | OrderContextView.NoPatient
            | OrderContextView.Settled _ -> false

        let select = ViewHelpers.filterSelect busy
        let autoComplete = ViewHelpers.autoComplete busy


        let progress = ViewHelpers.progressOrEmpty formulary

        let stackDirection =
            if Mui.Hooks.useMediaQuery "(max-width:900px)" then
                "column"
            else
                "row"

        let markdownBoxSx = {| color = Mui.Colors.Indigo.``900`` |}

        let doseCheckHeadingSx =
            {|
                marginTop = 2
                fontWeight = "bold"
                color = Mui.Colors.Indigo.``900``
            |}

        let doseCheckLineSx = {| marginTop = 0.5 |}

        let doseCheckAlertSx = {| marginTop = 1 |}

        let textOf tb =
            let items =
                match tb with
                | Valid xs
                | Caution xs
                | Warning xs
                | Alert xs -> xs

            items
            |> Array.map (
                function
                | Normal s
                | Bold s
                | Italic s -> s
            )
            |> String.concat ""

        let isAllValid (blocks: TextBlock[]) =
            blocks
            |> Array.forall (
                function
                | Valid _ -> true
                | _ -> false
            )

        let isAllCaution (blocks: TextBlock[]) =
            (blocks |> Array.isEmpty |> not)
            && blocks
               |> Array.forall (
                   function
                   | Caution _ -> true
                   | _ -> false
               )

        let renderDoseCheck (form: Formulary) =
            if form.DoseCheck |> Array.isEmpty then
                null |> toReact
            elif form.DoseCheck |> isAllValid then
                let text = form.DoseCheck |> Array.map textOf |> String.concat " "

                JSX.jsx
                    $"""
                    import Alert from '@mui/material/Alert';

                    <Box>
                        <Typography sx={doseCheckHeadingSx}>
                            Doseer controle volgens de G-Standaard
                        </Typography>
                        <Alert severity="success" sx={doseCheckAlertSx}>{text}</Alert>
                    </Box>
                    """
                |> toReact
            elif form.DoseCheck |> isAllCaution then
                let text = form.DoseCheck |> Array.map textOf |> String.concat " "

                JSX.jsx
                    $"""
                    import Alert from '@mui/material/Alert';

                    <Box>
                        <Typography sx={doseCheckHeadingSx}>
                            Doseer controle volgens de G-Standaard
                        </Typography>
                        <Alert severity="info" sx={doseCheckAlertSx}>{text}</Alert>
                    </Box>
                    """
                |> toReact
            else
                let lines =
                    form.DoseCheck
                    |> Array.map (fun tb ->
                        JSX.jsx
                            $"""
                            <Box sx={doseCheckLineSx}>
                                {tb |> Mui.TypoGraphy.fromTextBlock}
                            </Box>
                            """
                    )
                    |> unbox<seq<ReactElement>>
                    |> React.Fragment

                JSX.jsx
                    $"""
                    <Box>
                        <Typography sx={doseCheckHeadingSx}>
                            Doseer controle volgens de G-Standaard
                        </Typography>
                        {lines}
                    </Box>
                    """
                |> toReact

        let content =
            JSX.jsx
                $"""
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';
            import Paper from '@mui/material/Paper';

            <CardContent>
                <Stack direction="column" spacing={3}>

                    <Typography sx={ {| fontSize = 14 |} } color="text.secondary" gutterBottom>
                        {Formulary |> getTerm "Formularium"}
                    </Typography>
                    {match formulary with
                     | Resolved form -> false, form.Indication, form.Indications
                     | _ -> true, None, [||]
                     |> fun (isLoading, sel, items) ->
                         let lbl = Terms.``Formulary Indications`` |> getTerm "Indicaties"

                         if isMobile then
                             items
                             |> Array.map (fun s -> s, s)
                             |> select isLoading lbl state.Indication (IndicationChange >> dispatch)
                         else
                             items |> autoComplete isLoading lbl sel (IndicationChange >> dispatch)

                }
                    <Stack direction={stackDirection} spacing={3} >
                        {match formulary with
                         | Resolved form -> false, form.Generic, form.Generics
                         | _ -> true, None, [||]
                         |> fun (isLoading, sel, items) ->
                             let lbl = Terms.``Formulary Medications`` |> getTerm "Medicatie"

                             if isMobile then
                                 items
                                 |> Array.map (fun s -> s, s)
                                 |> select isLoading lbl state.Generic (GenericChange >> dispatch)
                             else
                                 items |> autoComplete isLoading lbl sel (GenericChange >> dispatch)}
                        {match formulary with
                         | Resolved form -> false, form.Route, form.Routes
                         | _ -> true, None, [||]
                         |> fun (isLoading, sel, items) ->
                             let lbl = Terms.``Formulary Routes`` |> getTerm "Routes"

                             if isMobile then
                                 items
                                 |> Array.map (fun s -> s, s)
                                 |> select isLoading lbl state.Route (RouteChange >> dispatch)
                             else
                                 items |> autoComplete isLoading lbl sel (RouteChange >> dispatch)}
                        {match formulary with
                         | Resolved form -> false, form.Form, form.Forms
                         | _ -> true, None, [||]
                         |> fun (isLoading, sel, items) ->
                             let lbl = Terms.``Pharmaceutical Form`` |> getTerm "Farmacologische Vorm"

                             if isMobile then
                                 items
                                 |> Array.map (fun s -> s, s)
                                 |> select isLoading lbl state.Route (FormChange >> dispatch)
                             else
                                 items |> autoComplete isLoading lbl sel (FormChange >> dispatch)}
                        {match formulary with
                         | Resolved form ->
                             (false, form.DoseType, form.DoseTypes)
                             |> fun (isLoading, sel, items) ->
                                 let lbl = Terms.``Dose Types`` |> getTerm "Doseer types"
                                 let sel = sel |> Option.map DoseType.doseTypeToString

                                 items
                                 |> Array.map (fun s -> s |> DoseType.doseTypeToString, s |> DoseType.doseTypeToDescription)
                                 |> select isLoading lbl sel (DoseTypeChange >> dispatch)

                         | _ -> null

                }
                    </Stack>

                    <Box sx={ {| marginTop = 2 |} }>
                        <Button variant="text" onClick={fun _ -> Clear |> dispatch} fullWidth startIcon={Mui.Icons.Delete} >
                            {Delete |> getTerm "Verwijder"}
                        </Button>
                    </Box>
                </Stack>

                <Box sx={markdownBoxSx} >
                    {match formulary with
                     | Resolved form ->
                         form.Markdown
                         |> Markdown.markdown.children
                         |> List.singleton
                         |> Feliz.Markdown.Markdown.markdown
                     | _ -> null |> toReact

                }
                </Box>

                {match formulary with
                 | Resolved form -> renderDoseCheck form
                 | _ -> null |> toReact}

            </CardContent>
            """

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Card from '@mui/material/Card';
        import CardActions from '@mui/material/CardActions';
        import CardContent from '@mui/material/CardContent';
        import Button from '@mui/material/Button';
        import Typography from '@mui/material/Typography';

        <Box>
                {content}
                {progress}
        </Box>
        """
