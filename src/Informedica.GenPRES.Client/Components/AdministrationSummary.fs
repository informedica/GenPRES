namespace Components


/// Shows how an order is given on one line: the order name, then a chip for each value, coloured by
/// the severity of the value.
module AdministrationSummary =

    open Informedica.GenPRES.Client.Core.Models


    open Fable.Core
    open Feliz
    open Informedica.GenPRES.Shared


    /// One value: its text and its severity. A value with an empty text shows no chip.
    type Part =
        {|
            text: string
            severity: Severity
        |}


    /// The order name and its values.
    type Props =
        {|
            name: string
            parts: Part[]
        |}


    // each chip sits in a cell of a least width, so the chips of the summaries above each other
    // line up as in a grid
    let private chipCellSx =
        {|
            display = "inline-flex"
            minWidth = "6.5rem"
            flexShrink = 0
        |}


    // the order name takes the same width in every summary, so the chips after it line up across
    // the summaries above each other; a longer name is cut off and shown in full on hover
    let private nameSx =
        {|
            color = "text.secondary"
            marginRight = 1
            width = "14rem"
            flexShrink = 0
            overflow = "hidden"
            textOverflow = "ellipsis"
            whiteSpace = "nowrap"
        |}


    let private rowSx =
        {|
            display = "inline-flex"
            alignItems = "center"
            flexWrap = "wrap"
            marginLeft = 1
        |}


    [<JSX.Component>]
    let View (props: Props) =
        let chips =
            props.parts
            |> Array.filter (fun p -> p.text <> "")
            |> Array.mapi (fun i p ->
                let chip =
                    ValueChip.View
                        {|
                            value = p.text
                            severity = p.severity
                            label = None
                        |}

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                <Box key={i} sx={chipCellSx}>{chip}</Box>
                """
            )
            |> unbox<seq<ReactElement>>
            |> React.Fragment

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Box sx={rowSx}>
            <Typography variant="body2" sx={nameSx} title={props.name}>
                {props.name}:
            </Typography>
            {chips}
        </Box>
        """
