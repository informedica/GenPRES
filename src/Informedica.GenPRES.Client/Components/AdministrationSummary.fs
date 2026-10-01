namespace Components


/// How an order is given, in a line: its name, then each value as a chip coloured by its
/// severity, such as the frequency, the dose, the rate and the time. The words between values,
/// such as "in" or "=", stay plain text, and a plus stands between the rows.
module AdministrationSummary =


    open Fable.Core
    open Feliz
    open Shared


    /// One piece of a row: its text and its severity.
    type Part =
        {|
            text: string
            severity: Types.Severity
        |}


    /// The summary: the order's name and the rows of parts.
    type Props =
        {|
            name: string
            rows: Part[][]
        |}


    let private textSx =
        {|
            display = "inline"
            color = "text.secondary"
            marginRight = 1
        |}


    let private rowSx =
        {|
            display = "inline-flex"
            alignItems = "center"
            flexWrap = "wrap"
            marginLeft = 1
        |}


    /// A part with a number in it is a value; one without is a name or a word between values.
    let isValue (text: string) = text |> Seq.exists System.Char.IsDigit


    [<JSX.Component>]
    let View (props: Props) =
        let text (key: string) (s: string) =
            JSX.jsx
                $"""
            import Typography from '@mui/material/Typography';
            <Typography key={key} variant="body2" sx={textSx}>{s}</Typography>
            """

        let ofRow (r: int) (row: Part[]) =
            row
            |> Array.filter (fun p -> p.text <> "")
            |> Array.mapi (fun i p ->
                if p.text |> isValue then
                    ValueChip.View
                        {|
                            value = p.text
                            severity = p.severity
                            label = None
                        |}
                else
                    text $"%i{r}-%i{i}" p.text
            )

        let parts =
            props.rows
            |> Array.filter (Array.exists (fun p -> p.text <> ""))
            |> Array.mapi (fun r row ->
                if r = 0 then
                    ofRow r row
                else
                    Array.append [| text $"sep-%i{r}" "+" |] (ofRow r row)
            )
            |> Array.concat
            |> unbox<seq<ReactElement>>
            |> React.Fragment

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Box sx={rowSx}>
            <Typography variant="body2" sx={textSx}>
                {props.name}:
            </Typography>
            {parts}
        </Box>
        """
