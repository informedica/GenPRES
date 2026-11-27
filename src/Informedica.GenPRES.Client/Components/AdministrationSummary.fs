namespace Components


/// Shows how an order is given on one line: the order name, then the parts of each row. A part
/// with a digit is a chip coloured by its severity, any other part is plain text such as "in" or
/// "=", and a plus separates the rows.
module AdministrationSummary =

    open Informedica.GenPRES.Client.Core.Models


    open Fable.Core
    open Feliz
    open Informedica.GenPRES.Shared


    /// One piece of a row: its text and its severity.
    type Part =
        {|
            text: string
            severity: Severity
        |}


    /// The order name and the rows of parts.
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


    /// Returns true when the text holds a digit, which marks a value rather than a word.
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
