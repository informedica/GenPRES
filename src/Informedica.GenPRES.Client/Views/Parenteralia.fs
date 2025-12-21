namespace Views


module Parenteralia =


    open Fable.Core
    open Feliz
    open Feliz.UseElmish
    open Elmish
    open Utils
    open Shared.Types
    open Shared
    open Shared.Models


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
            (state : State) : State * Cmd<Msg>
            =

            match msg with
            | Clear ->
                match parentaralia with
                | Resolved par ->
                    Parenteralia.empty
                    |> updateParenteralia
                | _ -> ()

                empty, Cmd.none

            | GenericChange s ->
                match parentaralia with
                | Resolved par ->
                    if s |> Option.isNone then Parenteralia.empty
                    else
                        { par with Generic = s }
                    |> updateParenteralia
                | _ -> ()

                { state with Generic = s }, Cmd.none

            | FormChange s ->
                match parentaralia with
                | Resolved par ->
                    if s |> Option.isNone then Parenteralia.empty
                    else
                        { par with Form = s }
                    |> updateParenteralia
                | _ -> ()

                { state with Form = s }, Cmd.none

            | RouteChange s ->
                match parentaralia with
                | Resolved par ->
                    if s |> Option.isNone then Parenteralia.empty
                    else
                        { par with Route = s }
                    |> updateParenteralia
                | _ -> ()

                { state with Route = s }, Cmd.none


    open Elmish


    [<JSX.Component>]
    let View (props:
        {|
            parenteralia: Deferred<Parenteralia>
            updateParenteralia: Parenteralia -> unit
        |}) =

        let context = React.useContext Global.context
        let lang = context.Localization
        let isMobile = Mui.Hooks.useMediaQuery "(max-width:900px)"

        let getTerm defVal term =
            //props.localizationTerms
            HasNotStartedYet
            |> Deferred.map (fun terms ->
                Localization.getTerm terms lang term
                |> Option.defaultValue defVal
            )
            |> Deferred.defaultValue defVal

        let state, dispatch =
            React.useElmish (
                init props.parenteralia,
                update props.parenteralia props.updateParenteralia,
                [|
                    box props.parenteralia
                |]
            )

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
            match props.parenteralia with
            | Resolved _ -> JSX.jsx $"<></>"
            | _ ->
                JSX.jsx
                    $"""
                import CircularProgress from '@mui/material/CircularProgress';

                <Box sx={ {| mt = 5; display = "flex"; p = 20 |} }>
                    <CircularProgress />
                </Box>
                """

        let stackDirection =
            if isMobile then "column" else "row"

        let content =
            JSX.jsx
                $"""
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';
            import Paper from '@mui/material/Paper';

            <CardContent>
                <Typography sx={ {| fontSize=14; pb=2 |} } color="text.secondary" gutterBottom>
                    {Terms.Formulary |> getTerm "Parenteralia"}
                </Typography>
                <Stack direction={stackDirection} spacing={3} >
                    {
                        match props.parenteralia with
                        | Resolved par -> false, par.Generic, par.Generics
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            if isMobile then
                                items
                                |> Array.map (fun s -> s, s)
                                |> select isLoading (Terms.``Formulary Medications`` |> getTerm "Medicatie") state.Generic (GenericChange >> dispatch)
                            else
                                items
                                |> autoComplete isLoading (Terms.``Formulary Medications`` |> getTerm "Medicatie") state.Generic (GenericChange >> dispatch)

                    }
                    {
                        match props.parenteralia with
                        | Resolved par -> false, par.Form, par.Forms
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            if items |> Array.isEmpty then JSX.jsx "<></>"
                            else
                                if isMobile then
                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select isLoading (Terms.``Formulary Indications`` |> getTerm "Forms") state.Form (FormChange >> dispatch)
                                else
                                    items
                                    |> autoComplete isLoading (Terms.``Formulary Indications`` |> getTerm "Forms") state.Form (FormChange >> dispatch)
                    }
                    {
                        match props.parenteralia with
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

                    <Box sx={ {| mt=2 |} }>
                        <Button variant="text" onClick={fun _ -> Clear |> dispatch } fullWidth startIcon={Mui.Icons.Delete} >
                            {Terms.Delete |> getTerm "Verwijder"}
                        </Button>
                        </Box>

                </Stack>
                <Box sx={ {| color = Mui.Colors.Indigo.``900`` |} } >
                    {
                        match props.parenteralia with
                        | Resolved par ->
                            par.Markdown
                            |> Markdown.markdown.children
                            |> List.singleton
                            |> Feliz.Markdown.Markdown.markdown
                        | _ ->
                            JSX.jsx "<></>"
                            |> toReact

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

        <Box sx={ {| height="100%" |} }>
                {content}
                {progress}
        </Box>
        """

