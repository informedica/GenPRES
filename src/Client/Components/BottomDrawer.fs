namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


open Elmish
open Fable.Core.JsInterop


module BottomDrawer =


    [<JSX.Component>]
    let View (props :
            {|
                isOpen : bool
                content : (string * ReactElement) []
            |}
        ) =

        let sx = {| margin="auto" |}
        JSX.jsx
            $"""
        import Drawer from '@mui/material/Drawer';
        import Stack from '@mui/material/Stack';
        <Drawer
            anchor="bottom"
            variant="persistent"
            open={props.isOpen}
        >
            <Stack sx={sx} direction="row" spacing={3} >
                {props.content |> withKey}
            </Stack>
        </Drawer>
        """
