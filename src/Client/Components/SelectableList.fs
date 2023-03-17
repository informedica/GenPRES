namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types



open Elmish
open Fable.Core.JsInterop


module SelectableList =


    [<JSX.Component>]
    let View (props : {| updateSelected : string -> unit; items : (string * bool)[] |}) =
        let items =
            props.items
            |> Array.mapi (fun i (text, selected) ->
                JSX.jsx
                    $"""
                import React from 'react';
                import Divider from '@mui/material/Divider';
                import ListItem from '@mui/material/ListItem';
                import ListItemButton from '@mui/material/ListItemButton';
                import ListItemIcon from '@mui/material/ListItemIcon';
                import ListItemText from '@mui/material/ListItemText';

                <React.Fragment key={i} >
                    <ListItem value={text} >
                        <ListItemButton selected={selected} onClick={fun _ -> text |> props.updateSelected}>
                        <ListItemText primary={text} />
                        </ListItemButton>
                    </ListItem>
                    <Divider></Divider>
                </React.Fragment>
                """
            )

        JSX.jsx
            $"""
        import List from '@mui/material/List';

        <List>
            {items}
        </List>
        """


