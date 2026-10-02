namespace Components


/// The controls of the user above a list, in one row: a filter that keeps the rows of a chosen
/// kind, and a search that narrows the list as it is typed. One of each and no more: a second
/// filter in the grid's own toolbar, beside the one above it, made it unclear whether the list
/// was filtered at all. The search and the filter are the caller's, built where their state is;
/// the row only places them, and draws nothing when it is given nothing.
module ListToolbar =


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    /// The row: the filter and the search, each when the list has one.
    type Props =
        {|
            search: JSX.Element option
            filter: JSX.Element option
        |}


    // the filter and the search are widened here, in the row above a list, and not in their own
    // components, which other pages use at their own width
    let private rowSx =
        createObj
            [
                "display" ==> "flex"
                "flexWrap" ==> "wrap"
                "alignItems" ==> "flex-end"
                "gap" ==> 2
                "marginBottom" ==> 2
                "flexShrink" ==> 0
                "& > .MuiFormControl-root" ==> createObj [ "minWidth" ==> 250 ]
            ]


    [<JSX.Component>]
    let View (props: Props) =
        match props.search, props.filter with
        | None, None -> null
        | _ ->
            let search = props.search |> Option.defaultValue null

            let filter = props.filter |> Option.defaultValue null

            JSX.jsx
                $"""
            import Box from '@mui/material/Box';

            <Box sx={rowSx}>
                {filter}
                {search}
            </Box>
            """
