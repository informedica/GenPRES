namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


open Elmish
open Fable.Core.JsInterop


module SideBar =


    [<JSX.Component>]
    let View (props :
            {|
                anchor : string
                isOpen : bool
                toggle : unit -> unit
                menuClick : string -> unit
                items : (string * bool)[]
            |}
        ) =
        let drawerWidth = 240

        let menu =
            {|
                updateSelected = props.menuClick
                items = props.items
            |}
            |> SelectableList.View

        JSX.jsx
            $"""
        import Drawer from '@mui/material/Drawer';

        <div>
            <Drawer
                anchor={props.anchor}
                width={drawerWidth}
                open={props.isOpen}
                onClose={props.toggle}
            >
            {menu}
            </Drawer>
        </div>
        """
