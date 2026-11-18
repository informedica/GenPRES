namespace Components


/// Places a filter and a search field in one row above a list. The caller builds both controls.
/// Shows nothing when neither is given. A list has only this one filter, so the user can always
/// see whether the list is filtered.
module ListToolbar =


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    /// The search field and the filter, each absent when the list has none.
    type Props =
        {|
            search: JSX.Element option
            filter: JSX.Element option
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
