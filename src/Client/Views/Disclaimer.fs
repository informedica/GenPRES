namespace Views

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types
open Shared.Types


module Disclaimer =

    let private text =
        """
ONLY FOR TEST/DEMO!!
This application is intended for use by healthcare professionals only.
It is not intended for use by patients or the general public.
The information provided is not intended to replace the advice of a healthcare professional.
The information provided is not intended to be used for the diagnosis or treatment of a health problem or disease.
Always consult with a healthcare professional if you have any concerns about your condition or treatment.
The information provided is not intended to be used for the diagnosis or treatment of a health problem or disease.
Always consult with a healthcare professional if you have any concerns about your condition or treatment.
This application has not been certified as a medical device and is not intended to be used as such.
        """

    open Shared

    [<JSX.Component>]
    let View(props: {|
            languages : Shared.Localization.Locales []
            switchLang : Shared.Localization.Locales -> unit
            localizationTerms : Deferred<string [] []>
            accept : bool -> unit
        |}) =

        let lang = React.useContext(Global.languageContext)

        let getTerm defVal term =
            props.localizationTerms
            |> Deferred.map (fun terms ->
                Localization.getTerm terms lang term
                |> Option.defaultValue defVal
            )
            |> Deferred.defaultValue defVal

        let langBtn =
            Components.Localization.View {|
                languages = props.languages
                switchLang = props.switchLang
            |}

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Card from '@mui/material/Card';
        import CardActions from '@mui/material/CardActions';
        import CardHeader from '@mui/material/CardHeader';
        import CardContent from '@mui/material/CardContent';
        import Button from '@mui/material/Button';
        import Typography from '@mui/material/Typography';

        <Card sx={ {| p=4 |} } variant="outlined">
            <CardHeader
                action={langBtn}
                title={(Disclaimer |> getTerm "Disclaimer").ToUpper()} />
            <CardContent>
                <Typography variant="body2">
                    {Terms.``Disclaimer text`` |> getTerm text}
                </Typography>
            </CardContent>
            <CardActions>
                <Button
                    variant="contained"
                    color="primary"
                    onClick={fun _ -> props.accept true}>
                    {Terms.``Disclaimer accept`` |> getTerm "I understand"}
                </Button>
            </CardActions>
        </Card>
        """

