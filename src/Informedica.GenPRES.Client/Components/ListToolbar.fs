namespace Components


/// Places a filter and a search field in one row above a list, with the actions of the list at the
/// right end of the row. The caller builds the controls and the actions. Shows nothing when none is
/// given. A list has only this one filter, so the user can always see whether the list is filtered.
module ListToolbar =


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    /// The search field, the filter and the actions, each absent when the list has none.
    type Props =
        {|
            search: JSX.Element option
            filter: JSX.Element option
            actions: JSX.Element option
        |}


    // The filter and the search are widened here and not in their own components, because other
    // pages use those components at their own width.
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


    // the actions take the free space before them, so they sit at the right end of the row
    let private actionsSx = {| marginLeft = "auto" |}


    [<JSX.Component>]
    let View (props: Props) =
        match props.search, props.filter, props.actions with
        | None, None, None -> null
        | _ ->
            let search = props.search |> Option.defaultValue null

            let filter = props.filter |> Option.defaultValue null

            let actions = props.actions |> Option.defaultValue null

            JSX.jsx
                $"""
            import Box from '@mui/material/Box';

            <Box sx={rowSx}>
                {filter}
                {search}
                <Box sx={actionsSx}>{actions}</Box>
            </Box>
            """
