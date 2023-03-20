namespace Views

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types
open Shared.Types


module Formulary =


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
                Indication: string option
                Route: string option
                Patient : string option
            }


        type Msg =
            | GenericChange of string option
            | IndicationChange of string option
            | RouteChange of string option
            | PatientChange of string option


        let empty =
            {
                Generic = None
                Indication = None
                Route = None
                Patient = None
            }


        let init (form: Deferred<Formulary>) =
            let state =
                match form with
                | Resolved form ->
                    {
                        Generic = form.Generic
                        Indication = form.Indication
                        Patient = form.Patient
                        Route = form.Route
                    }
                | _ -> empty

            state, Cmd.none


        let update
            (form: Deferred<Formulary>)
            updateFormulary
            (msg: Msg)
            state : State * Cmd<Msg>
            =
            let clear (form : Formulary) =
                { form with
                    Indication = None
                    Generic = None
                    Route = None
                    Patient = None
                }

            match msg with
            | GenericChange s ->
                match form with
                | Resolved form ->
                    if s |> Option.isNone then Formulary.empty
                    else
                        { form with Generic = s }
                    |> updateFormulary
                | _ -> ()

                { state with Generic = s }, Cmd.none

            | IndicationChange s ->
                printfn $"indication change {s}"
                match form with
                | Resolved form ->
                    if s |> Option.isNone then Formulary.empty
                    else
                        { form with Indication = s }
                    |> updateFormulary
                | _ -> ()

                { state with Indication = s }, Cmd.none

            | RouteChange s ->
                match form with
                | Resolved form ->
                    if s |> Option.isNone then Formulary.empty
                    else
                        { form with Route = s }
                    |> updateFormulary
                | _ -> ()

                { state with Route = s }, Cmd.none

            | PatientChange s ->
                match form with
                | Resolved form ->
                    if s |> Option.isNone then Formulary.empty
                    else
                        { form with Patient = s }
                    |> updateFormulary
                | _ -> ()

                { state with Patient = s }, Cmd.none


    open Elmish


    [<JSX.Component>]
    let View (props:
        {|
            formulary: Deferred<Formulary>
            updateFormulary: Formulary -> unit
        |}) =

        let state, dispatch =
            React.useElmish (
                init props.formulary,
                update props.formulary props.updateFormulary,
                [| box props.formulary; box props.formulary |]
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
            match props.formulary with
            | Resolved _ -> JSX.jsx $"<></>"
            | _ ->
                JSX.jsx
                    $"""
                import CircularProgress from '@mui/material/CircularProgress';

                <Box sx={ {| mt = 5; display = "flex"; p = 20 |} }>
                    <CircularProgress />
                </Box>
                """

        let typoGraphy (items : Shared.Types.TextItem[]) =
            let print item =
                match item with
                | Shared.Types.Normal s ->
                    JSX.jsx
                        $"""
                    <Typography color={Mui.Colors.Grey.``700``} display="inline">{s}</Typography>
                    """
                | Shared.Types.Bold s ->
                    JSX.jsx
                        $"""
                    <Typography
                    color={Mui.Colors.BlueGrey.``700``}
                    display="inline"
                    >
                    <strong> {s} </strong>
                    </Typography>
                    """
                | Shared.Types.Italic s ->
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
                {items |> Array.map print}
            </Box>
            """

        let content =
            JSX.jsx
                $"""
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';
            import Paper from '@mui/material/Paper';

            <CardContent>
                <Typography sx={ {| fontSize=14 |} } color="text.secondary" gutterBottom>
                    Formularium
                </Typography>
                <Stack direction="column" spacing={3} >
                    {
                        match props.formulary with
                        | Resolved form -> false, form.Generic, form.Generics
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading "Generieken" sel (GenericChange >> dispatch)
                    }
                    {
                        match props.formulary with
                        | Resolved form -> false, form.Indication, form.Indications
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading "Indicaties" sel (IndicationChange >> dispatch)
                    }
                    {
                        match props.formulary with
                        | Resolved form -> false, form.Route, form.Routes
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading "Routes" sel (RouteChange >> dispatch)
                    }
                    {
                        match props.formulary with
                        | Resolved form -> false, form.Patient, form.Patients
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading "Patienten" sel (PatientChange >> dispatch)
                    }
                </Stack>
                <Paper sx={ {| mt = 2; color = Mui.Colors.Indigo.``900`` |} } >
                    {
                        match props.formulary with
                        | Resolved form ->
                            form.Markdown
                            |> Markdown.markdown.children
                            |> List.singleton
                            |> Feliz.Markdown.Markdown.markdown
                        | _ ->
                            JSX.jsx "<></>"
                            |> toReact

                    }
                </Paper>
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


