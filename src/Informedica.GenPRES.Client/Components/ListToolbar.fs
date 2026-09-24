namespace Components


/// The controls of the user above a list, in one row: a search that narrows the list as it is
/// typed, and a filter that keeps the rows of a chosen kind. One of each and no more: a second
/// filter in the grid's own toolbar, beside the one above it, made it unclear whether the list
/// was filtered at all. The search and the filter are the caller's, built where their state is;
/// the row only places them, and draws nothing when it is given nothing.
module ListToolbar =


    open Fable.Core
    open Feliz


    /// The row: the search and the filter, each when the list has one.
    type Props =
        {|
            search: JSX.Element option
            filter: JSX.Element option
        |}


    let private rowSx =
        {|
            display = "flex"
            flexWrap = "wrap"
            alignItems = "flex-end"
            gap = 2
            marginBottom = 2
            flexShrink = 0
        |}


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
                {search}
                {filter}
            </Box>
            """
