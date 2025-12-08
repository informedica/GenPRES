namespace Components


module SideMenu =


    open Fable.Core


    [<JSX.Component>]
    let View (props :
            {|
                anchor : string
                isOpen : bool
                toggle : unit -> unit
                menuClick : string -> unit
                items : (JSX.Element option * string * bool)[]
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
