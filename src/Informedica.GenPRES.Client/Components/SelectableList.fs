namespace Components



module SelectableList =


    open Fable.Core


    [<JSX.Component>]
    let View (props : {| updateSelected : string -> unit; items : (JSX.Element option * string * bool)[] |}) =
        let items =
            props.items
            |> Array.mapi (fun i (el, text, selected) ->
                let icon =
                    match el with
                    | None -> JSX.jsx "<></>"
                    | Some el ->
                        JSX.jsx
                            $"""
                        import ListItemIcon from '@mui/material/ListItemIcon';
                        <ListItemIcon>{el}</ListItemIcon>
                        """

                JSX.jsx
                    $"""
                import React from 'react';
                import Divider from '@mui/material/Divider';
                import ListItem from '@mui/material/ListItem';
                import ListItemButton from '@mui/material/ListItemButton';
                import ListItemText from '@mui/material/ListItemText';

                <React.Fragment key={i} >
                    <ListItem value={text} >
                        {icon}
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


