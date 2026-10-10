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


        type Msg =
            | GenericChange of string option
            | IndicationChange of string option
            | RouteChange of string option
            | FormChange of string option
            | DoseTypeChange of string option
            | Clear


        /// The formulary with one choice applied, sent to the App: the selects read the formulary
        /// the App holds, never a copy of their own, so what they show is what is filtered on.
        let update (formulary: Deferred<Formulary>) updateFormulary (msg: Msg) : unit =
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

            | DoseTypeChange s ->
                match formulary with
                | Resolved form ->
                    { form with DoseType = s |> Option.map DoseType.doseTypeFromString }
                    |> updateFormulary
                | _ -> ()

    open Elmish


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envFormulary = AppEnv.asEnv<AppEnv.IFormulary> props.appEnv
        let formulary = envFormulary.Formulary
        let updateFormulary = envFormulary.UpdateFormulary

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let isMobile = Mui.Hooks.useIsMobile ()

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let dispatch msg = msg |> update formulary updateFormulary

        let select = ViewHelpers.filterSelect
        let autoComplete = ViewHelpers.autoComplete


        let patientNotice =
            Components.PatientNotice.View
                {|
                    appEnv = props.appEnv
                    needs = PatientReadiness.Needs.DoseCheck
                |}

        let stackDirection = if Mui.Hooks.useIsNarrow () then "column" else "row"

        let markdownBoxSx = {| color = Mui.Colors.Indigo.``900`` |}

        let doseCheckHeadingSx =
            {|
                marginTop = 2
                fontWeight = "bold"
                color = Mui.Colors.Indigo.``900``
            |}

        let doseCheckLineSx = {| marginTop = 0.5 |}

        let doseCheckAlertSx = {| marginTop = 1 |}

        let textOf = ViewHelpers.textBlockText

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

                let notice =
                    Components.Notice.View
                        {|
                            kind = Components.Notice.Kind.Success
                            title = None
                            message = text
                            action = None
                            onClose = None
                        |}

                JSX.jsx
                    $"""
                    <Box>
                        <Typography sx={doseCheckHeadingSx}>
                            Doseer controle volgens de G-Standaard
                        </Typography>
                        <Box sx={doseCheckAlertSx}>{notice}</Box>
                    </Box>
                    """
                |> toReact
            elif form.DoseCheck |> isAllCaution then
                let text = form.DoseCheck |> Array.map textOf |> String.concat " "

                let notice =
                    Components.Notice.View
                        {|
                            kind = Components.Notice.Kind.Info
                            title = None
                            message = text
                            action = None
                            onClose = None
                        |}

                JSX.jsx
                    $"""
                    <Box>
                        <Typography sx={doseCheckHeadingSx}>
                            Doseer controle volgens de G-Standaard
                        </Typography>
                        <Box sx={doseCheckAlertSx}>{notice}</Box>
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
                     | Resolved form
                     | Refreshing form -> form.Indication, form.Indications
                     | _ -> None, [||]
                     |> fun (sel, items) ->
                         let lbl = Terms.``Formulary Indications`` |> getTerm "Indicaties"

                         if isMobile then
                             items
                             |> Array.map (fun s -> s, s)
                             |> select lbl sel (IndicationChange >> dispatch)
                         else
                             items |> autoComplete lbl sel (IndicationChange >> dispatch)

                }
                    <Stack direction={stackDirection} spacing={3} >
                        {match formulary with
                         | Resolved form
                         | Refreshing form -> form.Generic, form.Generics
                         | _ -> None, [||]
                         |> fun (sel, items) ->
                             let lbl = Terms.``Formulary Medications`` |> getTerm "Medicatie"

                             if isMobile then
                                 items |> Array.map (fun s -> s, s) |> select lbl sel (GenericChange >> dispatch)
                             else
                                 items |> autoComplete lbl sel (GenericChange >> dispatch)}
                        {match formulary with
                         | Resolved form
                         | Refreshing form -> form.Route, form.Routes
                         | _ -> None, [||]
                         |> fun (sel, items) ->
                             let lbl = Terms.``Formulary Routes`` |> getTerm "Routes"

                             if isMobile then
                                 items |> Array.map (fun s -> s, s) |> select lbl sel (RouteChange >> dispatch)
                             else
                                 items |> autoComplete lbl sel (RouteChange >> dispatch)}
                        {match formulary with
                         | Resolved form
                         | Refreshing form -> form.Form, form.Forms
                         | _ -> None, [||]
                         |> fun (sel, items) ->
                             let lbl = Terms.``Pharmaceutical Form`` |> getTerm "Farmacologische Vorm"

                             if isMobile then
                                 items |> Array.map (fun s -> s, s) |> select lbl sel (FormChange >> dispatch)
                             else
                                 items |> autoComplete lbl sel (FormChange >> dispatch)}
                        {match formulary with
                         | Resolved form
                         | Refreshing form ->
                             (form.DoseType, form.DoseTypes)
                             |> fun (sel, items) ->
                                 let lbl = Terms.``Dose Types`` |> getTerm "Doseer types"
                                 let sel = sel |> Option.map DoseType.doseTypeToString

                                 items
                                 |> Array.map (fun s -> s |> DoseType.doseTypeToString, s |> DoseType.doseTypeToDescription)
                                 |> select lbl sel (DoseTypeChange >> dispatch)

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
                     | Resolved form
                     | Refreshing form ->
                         form.Markdown
                         |> Markdown.markdown.children
                         |> List.singleton
                         |> Feliz.Markdown.Markdown.markdown
                     | _ -> null |> toReact

                }
                </Box>

                {match formulary with
                 | Resolved form
                 | Refreshing form -> renderDoseCheck form
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
                {patientNotice}
                {content}
        </Box>
        """
