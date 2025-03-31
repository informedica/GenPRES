namespace Views


module Formulary =

    open Fable.Core
    open Fable.React
    open Feliz
    open Shared
    open Shared.Models
    open Shared.Types
    open Elmish


    module private Elmish =



        type State =
            {
                Generic: string option
                Indication: string option
                Route: string option
                Shape : string option
                DoseType: string option
            }


        type Msg =
            | GenericChange of string option
            | IndicationChange of string option
            | RouteChange of string option
            | ShapeChange of string option
            | DoseTypeChange of string option
            | Clear


        let empty =
            {
                Generic = None
                Indication = None
                Route = None
                Shape = None
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
                        Shape = form.Shape
                        DoseType = form.DoseType |> Option.map  DoseType.doseTypeToDescription
                    }
                | _ -> empty

            state, Cmd.none


        let update
            (formulary: Deferred<Formulary>)
            updateFormulary
            (msg: Msg)
            (state : State) : State * Cmd<Msg>
            =
            let clear (form : Formulary) =
                { form with
                    Indications = [||]
                    Indication = None
                    Generics = [||]
                    Generic = None
                    Routes = [||]
                    Route = None
                    Shapes = [||]
                    Shape = None
                    DoseTypes = [||]
                    DoseType = None
                    PatientCategories = [||]
                    PatientCategory = None
                }

            match msg with
            | Clear ->
                match formulary with
                | Resolved form ->
                    form
                    |> clear
                    |> updateFormulary
                | _ -> ()
                { state with
                    Indication = None
                    Generic = None
                    Route = None
                    DoseType = None
                }, Cmd.none

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
                            Shape = None
                            Shapes = [||]
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
                        Shapes = [||]
                        Shape = None
                        Route = s
                    }
                    |> updateFormulary
                | _ -> ()

                { state with Route = s }, Cmd.none

            | ShapeChange s ->
                match formulary with
                | Resolved form ->
                    { form with
                        DoseType = None
                        DoseTypes = [||]
                        Shape = s
                    }
                    |> updateFormulary
                | _ -> ()

                { state with Shape = s }, Cmd.none

            | DoseTypeChange s ->
                match formulary with
                | Resolved form ->
                    { form with
                        DoseType = s |> Option.map DoseType.doseTypeFromString
                    }
                    |> updateFormulary
                | _ -> ()

                { state with DoseType = s }, Cmd.none

    open Elmish


    [<JSX.Component>]
    let View (props:
        {|
            formulary: Deferred<Formulary>
            updateFormulary: Formulary -> unit
            localizationTerms : Deferred<string [] []>
        |}) =

        let context = React.useContext(Global.context)
        let lang = context.Localization
        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let getTerm defVal term =
            props.localizationTerms
            |> Deferred.map (fun terms ->
                Localization.getTerm terms lang term
                |> Option.defaultValue defVal
            )
            |> Deferred.defaultValue defVal

        let state, dispatch =
            React.useElmish (
                init props.formulary,
                update props.formulary props.updateFormulary,
                [|
                    box props.formulary
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
                <Stack direction="column" spacing={3}>

                    <Typography sx={ {| fontSize=14 |} } color="text.secondary" gutterBottom>
                        {Formulary |> getTerm "Formularium"}
                    </Typography>
                    {
                        match props.formulary with
                        | Resolved form -> false, form.Indication, form.Indications
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            let lbl = Terms.``Formulary Indications`` |> getTerm "Indicaties"
                            if isMobile then
                                items
                                |> Array.map (fun s -> s, s)
                                |> select isLoading lbl state.Indication (IndicationChange >> dispatch)
                            else
                                items
                                |> autoComplete isLoading lbl sel (IndicationChange >> dispatch)

                    }
                    <Stack direction={stackDirection} spacing={3} >
                        {
                            match props.formulary with
                            | Resolved form -> false, form.Generic, form.Generics
                            | _ -> true, None, [||]
                            |> fun (isLoading, sel, items) ->
                                let lbl = Terms.``Formulary Medications`` |> getTerm "Medicatie"
                                if isMobile then
                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select isLoading lbl state.Generic (GenericChange >> dispatch)
                                else
                                    items
                                    |> autoComplete isLoading lbl sel (GenericChange >> dispatch)
                        }
                        {
                            match props.formulary with
                            | Resolved form -> false, form.Route, form.Routes
                            | _ -> true, None, [||]
                            |> fun (isLoading, sel, items) ->
                                let lbl = Terms.``Formulary Routes`` |> getTerm "Routes"
                                if isMobile then
                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select isLoading lbl state.Route (RouteChange >> dispatch)
                                else
                                    items
                                    |> autoComplete isLoading lbl sel (RouteChange >> dispatch)
                        }
                        {
                            match props.formulary with
                            | Resolved form -> false, form.Shape, form.Shapes
                            | _ -> true, None, [||]
                            |> fun (isLoading, sel, items) ->
                                let lbl = "Farmacologische Vorm"
                                if isMobile then
                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select isLoading lbl state.Route (ShapeChange >> dispatch)
                                else
                                    items
                                    |> autoComplete isLoading lbl sel (ShapeChange >> dispatch)
                        }
                        {
                            match props.formulary with
                            | Resolved form ->
                                (false, form.DoseType, form.DoseTypes)
                                |> fun (isLoading, sel, items) ->
                                        let lbl = "Doseer types"
                                        let sel = sel |> Option.map DoseType.doseTypeToString

                                        items
                                        |> Array.map (fun s -> s |> DoseType.doseTypeToString, s |> DoseType.doseTypeToDescription)
                                        |> select isLoading lbl sel (DoseTypeChange >> dispatch)

                            | _ -> JSX.jsx $"<></>"

                        }
                    </Stack>

                    <Box sx={ {| mt=2 |} }>
                        <Button variant="text" onClick={fun _ -> Clear |> dispatch } fullWidth startIcon={Mui.Icons.Delete} >
                            {Delete |> getTerm "Verwijder"}
                        </Button>
                    </Box>
                </Stack>

                <Box sx={ {| color = Mui.Colors.Indigo.``900`` |} } >
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

        <Box sx={ {| overflowY="auto" |} }>
                {content}
                {progress}
        </Box>
        """


