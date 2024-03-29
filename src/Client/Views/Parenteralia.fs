namespace Views

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types
open Shared.Types


module Parenteralia =


    module private Elmish =

        open Feliz
        open Feliz.UseElmish
        open Elmish
        open Shared
        open Utils
        open FSharp.Core


        type State =
            {
                Generic: string option
                Shape: string option
                Route: string option
            }


        type Msg =
            | GenericChange of string option
            | ShapeChange of string option
            | RouteChange of string option


        let empty =
            {
                Generic = None
                Shape = None
                Route = None
            }


        let init (par: Deferred<Parenteralia>) =
            let state =
                match par with
                | Resolved form ->
                    {
                        Generic = form.Generic //|> Option.orElse gen
                        Shape = form.Shape //|> Option.orElse ind
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
            let clear (par : Parenteralia) =
                { par with
                    Shape = None
                    Generic = None
                    Route = None
                    Patient = None
                }

            match msg with  
            | GenericChange s ->
                match parentaralia with
                | Resolved par ->
                    if s |> Option.isNone then Parenteralia.empty
                    else
                        { par with Generic = s }
                    |> updateParenteralia
                | _ -> ()

                { state with Generic = s }, Cmd.none

            | ShapeChange s ->
                match parentaralia with
                | Resolved par ->
                    if s |> Option.isNone then Parenteralia.empty
                    else
                        { par with Shape = s }
                    |> updateParenteralia
                | _ -> ()

                { state with Shape = s }, Cmd.none

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
    open Shared


    [<JSX.Component>]
    let View (props:
        {|
            parenteralia: Deferred<Parenteralia>
            updateParenteralia: Parenteralia -> unit
        |}) =

        let context = React.useContext(Global.context)
        let lang = context.Localization

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
            if  Mui.Hooks.useMediaQuery "(max-width:900px)" then "column" else "row"

        let content =
            JSX.jsx
                $"""
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';
            import Paper from '@mui/material/Paper';

            <CardContent>
                <Typography sx={ {| fontSize=14 |} } color="text.secondary" gutterBottom>
                    {Terms.Formulary |> getTerm "Formularium"}
                </Typography>
                <Stack direction={stackDirection} spacing={3} >
                    {
                        match props.parenteralia with
                        | Resolved par -> false, par.Generic, par.Generics
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading (Terms.``Formulary Medications`` |> getTerm "Medicatie") state.Generic (GenericChange >> dispatch)
                    }
                    {
                        match props.parenteralia with
                        | Resolved par -> false, par.Shape, par.Shapes
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading (Terms.``Formulary Indications`` |> getTerm "Shapes") state.Shape (ShapeChange >> dispatch)
                    }
                    {
                        match props.parenteralia with
                        | Resolved par -> false, par.Route, par.Routes
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading (Terms.``Formulary Routes`` |> getTerm "Routes") state.Route (RouteChange >> dispatch)
                    }
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

