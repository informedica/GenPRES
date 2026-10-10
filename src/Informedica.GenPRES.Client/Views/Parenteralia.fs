namespace Views


module Parenteralia =


    open Fable.Core
    open Feliz
    open Feliz.UseElmish
    open Elmish
    open Shared.Types
    open Shared
    open Shared.Models
    open OrderContextMachine


    module private Elmish =


        type Msg =
            | Clear
            | GenericChange of string option
            | FormChange of string option
            | RouteChange of string option


        /// The parenteralia filter with one choice applied, sent to the App: the selects read the
        /// filter the App holds, never a copy of their own, so what they show is what is filtered on.
        let update (parentaralia: Deferred<Parenteralia>) updateParenteralia (msg: Msg) : unit =

            match msg with
            | Clear ->
                match parentaralia with
                | Resolved par -> Parenteralia.empty |> updateParenteralia
                | _ -> ()

            | GenericChange s ->
                match parentaralia with
                | Resolved par ->
                    if s |> Option.isNone then
                        Parenteralia.empty
                    else
                        { par with Generic = s }
                    |> updateParenteralia
                | _ -> ()

            | FormChange s ->
                match parentaralia with
                | Resolved par ->
                    if s |> Option.isNone then
                        Parenteralia.empty
                    else
                        { par with Form = s }
                    |> updateParenteralia
                | _ -> ()

            | RouteChange s ->
                match parentaralia with
                | Resolved par ->
                    if s |> Option.isNone then
                        Parenteralia.empty
                    else
                        { par with Route = s }
                    |> updateParenteralia
                | _ -> ()


    open Elmish


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envParenteralia = AppEnv.asEnv<AppEnv.IParenteralia> props.appEnv
        let parenteralia = envParenteralia.Parenteralia
        let updateParenteralia = envParenteralia.UpdateParenteralia

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let isMobile = Mui.Hooks.useIsNarrow ()

        let getTerm = Global.getLocalizedTerm HasNotStartedYet lang

        let dispatch msg = msg |> update parenteralia updateParenteralia

        let responsiveFilter = ViewHelpers.responsiveFilter isMobile

        let patientNotice =
            Components.PatientNotice.View
                {|
                    appEnv = props.appEnv
                    needs = PatientReadiness.Needs.DoseCheck
                |}

        let stackDirection = if isMobile then "column" else "row"

        let content =
            let subtitleSx =
                {|
                    fontSize = 14
                    pb = 2
                |}

            JSX.jsx
                $"""
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';
            import Paper from '@mui/material/Paper';

            <CardContent>
                <Typography sx={subtitleSx} color="text.secondary" gutterBottom>
                    {Terms.Formulary |> getTerm "Parenteralia"}
                </Typography>
                <Stack direction={stackDirection} spacing={3} >
                    {match parenteralia with
                     | Resolved par
                     | Refreshing par -> par.Generic, par.Generics
                     | _ -> None, [||]
                     |> fun (sel, items) ->
                         items
                         |> responsiveFilter (Terms.``Formulary Medications`` |> getTerm "Medicatie") sel (GenericChange >> dispatch)

                }
                    {match parenteralia with
                     | Resolved par
                     | Refreshing par -> par.Form, par.Forms
                     | _ -> None, [||]
                     |> fun (sel, items) ->
                         if items |> Array.isEmpty then
                             null
                         else
                             items
                             |> responsiveFilter (Terms.``Formulary Indications`` |> getTerm "Forms") sel (FormChange >> dispatch)}
                    {match parenteralia with
                     | Resolved par
                     | Refreshing par -> par.Route, par.Routes
                     | _ -> None, [||]
                     |> fun (sel, items) ->
                         items
                         |> responsiveFilter (Terms.``Formulary Routes`` |> getTerm "Routes") sel (RouteChange >> dispatch)

                }

                    <Box sx={ {| marginTop = 2 |} }>
                        <Button variant="text" onClick={fun _ -> Clear |> dispatch} fullWidth startIcon={Mui.Icons.Delete} >
                            {Terms.Delete |> getTerm "Verwijder"}
                        </Button>
                        </Box>

                </Stack>
                <Box sx={ {| color = Mui.Colors.Indigo.``900`` |} } >
                    {match parenteralia with
                     | Resolved par
                     | Refreshing par ->
                         par.Markdown
                         |> Markdown.markdown.children
                         |> List.singleton
                         |> Feliz.Markdown.Markdown.markdown
                     | _ -> null

                }
                </Box>
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

        <Box sx={ {| height = "100%" |} }>
                {patientNotice}
                {content}
        </Box>
        """
