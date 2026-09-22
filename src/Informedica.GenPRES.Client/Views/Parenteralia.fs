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


        type State =
            {
                Generic: string option
                Form: string option
                Route: string option
            }


        type Msg =
            | Clear
            | GenericChange of string option
            | FormChange of string option
            | RouteChange of string option


        let empty =
            {
                Generic = None
                Form = None
                Route = None
            }


        let init (par: Deferred<Parenteralia>) =
            let state =
                match par with
                | Resolved form ->
                    {
                        Generic = form.Generic //|> Option.orElse gen
                        Form = form.Form //|> Option.orElse ind
                        Route = form.Route //|> Option.orElse rte
                    }
                | _ -> empty

            state, Cmd.none


        let update
            (parentaralia: Deferred<Parenteralia>)
            updateParenteralia
            (msg: Msg)
            (state: State)
            : State * Cmd<Msg>
            =

            match msg with
            | Clear ->
                match parentaralia with
                | Resolved par -> Parenteralia.empty |> updateParenteralia
                | _ -> ()

                empty, Cmd.none

            | GenericChange s ->
                match parentaralia with
                | Resolved par ->
                    if s |> Option.isNone then
                        Parenteralia.empty
                    else
                        { par with Generic = s }
                    |> updateParenteralia
                | _ -> ()

                { state with Generic = s }, Cmd.none

            | FormChange s ->
                match parentaralia with
                | Resolved par ->
                    if s |> Option.isNone then
                        Parenteralia.empty
                    else
                        { par with Form = s }
                    |> updateParenteralia
                | _ -> ()

                { state with Form = s }, Cmd.none

            | RouteChange s ->
                match parentaralia with
                | Resolved par ->
                    if s |> Option.isNone then
                        Parenteralia.empty
                    else
                        { par with Route = s }
                    |> updateParenteralia
                | _ -> ()

                { state with Route = s }, Cmd.none


    open Elmish


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envParenteralia = AppEnv.asEnv<AppEnv.IParenteralia> props.appEnv
        let parenteralia = envParenteralia.Parenteralia
        let updateParenteralia = envParenteralia.UpdateParenteralia

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let isMobile = Mui.Hooks.useMediaQuery "(max-width:900px)"

        let getTerm = Global.getLocalizedTerm HasNotStartedYet lang

        let state, dispatch =
            React.useElmish (init parenteralia, update parenteralia updateParenteralia, [| box parenteralia |])

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

        let progress = ViewHelpers.progressOrEmpty parenteralia

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
                     | Resolved par -> false, par.Generic, par.Generics
                     | _ -> true, None, [||]
                     |> fun (isLoading, sel, items) ->
                         if isMobile then
                             items
                             |> Array.map (fun s -> s, s)
                             |> select
                                 isLoading
                                 (Terms.``Formulary Medications`` |> getTerm "Medicatie")
                                 state.Generic
                                 (GenericChange >> dispatch)
                         else
                             items
                             |> autoComplete
                                 isLoading
                                 (Terms.``Formulary Medications`` |> getTerm "Medicatie")
                                 state.Generic
                                 (GenericChange >> dispatch)

                }
                    {match parenteralia with
                     | Resolved par -> false, par.Form, par.Forms
                     | _ -> true, None, [||]
                     |> fun (isLoading, sel, items) ->
                         if items |> Array.isEmpty then
                             null
                         else if isMobile then
                             items
                             |> Array.map (fun s -> s, s)
                             |> select isLoading (Terms.``Formulary Indications`` |> getTerm "Forms") state.Form (FormChange >> dispatch)
                         else
                             items
                             |> autoComplete
                                 isLoading
                                 (Terms.``Formulary Indications`` |> getTerm "Forms")
                                 state.Form
                                 (FormChange >> dispatch)}
                    {match parenteralia with
                     | Resolved par -> false, par.Route, par.Routes
                     | _ -> true, None, [||]
                     |> fun (isLoading, sel, items) ->
                         if isMobile then
                             items
                             |> Array.map (fun s -> s, s)
                             |> select isLoading (Terms.``Formulary Routes`` |> getTerm "Routes") state.Route (RouteChange >> dispatch)
                         else
                             items
                             |> autoComplete isLoading (Terms.``Formulary Routes`` |> getTerm "Routes") state.Route (RouteChange >> dispatch)

                }

                    <Box sx={ {| marginTop = 2 |} }>
                        <Button variant="text" onClick={fun _ -> Clear |> dispatch} fullWidth startIcon={Mui.Icons.Delete} >
                            {Terms.Delete |> getTerm "Verwijder"}
                        </Button>
                        </Box>

                </Stack>
                <Box sx={ {| color = Mui.Colors.Indigo.``900`` |} } >
                    {match parenteralia with
                     | Resolved par ->
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
                {content}
                {progress}
        </Box>
        """
