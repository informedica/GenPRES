namespace Components


module BottomDrawer =


    open Fable.Core
    open Feliz


    [<JSX.Component>]
    let View
        (props:
            {|
                isOpen: bool
                content: (string * ReactElement)[]
            |})
        =
        let sx =
            {|
                margin = "auto"
                paddingTop = 2
                paddingBottom = 2
            |}

        let drawerSx = {| ``& .MuiDrawer-paper`` = {| bgcolor = Mui.Colors.Grey.``100`` |} |}

        JSX.jsx
            $"""
        import Drawer from '@mui/material/Drawer';
        import Stack from '@mui/material/Stack';
        <Drawer
            anchor="bottom"
            variant="persistent"
            open={props.isOpen}
            sx={drawerSx}
        >
            <Stack sx={sx} direction="row" spacing={3} >
                {props.content |> withKey}
            </Stack>
        </Drawer>
        """
