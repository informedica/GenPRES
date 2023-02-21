namespace Components

module Filter =

    open Elmish
    open Feliz
    open MaterialUI5
    open FSharp.Core
    open Feliz.UseElmish

    type Model = {
        DialogOpen: bool
        Selected : string []
    }

    type Msg =
    //| Filter
    | SetDialogState of bool

    let useStyles =
        Styles.makeStyles (fun styles theme ->
            {|
                formControl =
                    styles.create [
                        style.minWidth "115px"
                        style.margin 10
                    ]
            |}
        )

    let init() =
            {
                DialogOpen = true
                Selected = [||]
            }

    // the update function is intitiated with a handle to
    // report back to the parent which item is selected.
    let update msg state =
        match msg with
            | SetDialogState s -> { state with DialogOpen = not state.DialogOpen}, Cmd.none

    [<ReactComponent>]
    let view(items : Map<string, string list>, state, dispatch) : ReactElement =

        let classes = useStyles ()

        //let state, dispatch = React.useElmish (init, update state)

        Html.div[
            Mui.formControl [
                prop.className classes.formControl
                formControl.margin.dense
                formControl.children[
                    Mui.button[
                        prop.text "Filter"
                        button.endIcon Icons.filterListIcon[]
                        button.variant.outlined
                    ]
                ]
            ]

            // Mui.formControl [
            //     prop.className classes.formControl
            //     formControl.margin.dense
            //     formControl.children[
            //         Mui.select[
            //             select.multiple true
            //             select.value state.Selected
            //             select.children[
            //                 for key in items |> Map.keys do
            //                     Mui.listSubheader[
            //                         prop.text key
            //                     ]
            //                     for value in items |> Map.find key do
            //                         Mui.menuItem[
            //                             typography.variant.h6
            //                             prop.text value
            //                             prop.onClick (fun item -> dispatch item)
            //                         ]
            //             ]
            //         ]
            //     ]
            // ]


            Mui.dialog[
                dialog.open' true
                dialog.scroll.paper
                dialog.children[
                    Mui.dialogTitle "Filter"
                    Mui.dialogContent[
                        Mui.grid[
                            grid.container
                            grid.spacing 2
                            grid.direction.row
                            grid.children[
                                for key in items |> Map.keys do
                                    Mui.grid[
                                        grid.item
                                        grid.xs 6
                                        grid.children[
                                            Mui.typography[
                                                typography.variant.h4
                                                prop.text key
                                            ]
                                            Mui.divider[]
                                            for value in items |> Map.find key do
                                                Mui.formGroup[
                                                    prop.children[
                                                        Mui.formControlLabel[
                                                            formControlLabel.control Mui.checkbox[
                                                                checkbox.id value
                                                            ]
                                                            formControlLabel.label value
                                                        ]
                                                    ]
                                                ]
                                        ]
                                    ]
                            ]
                        ]
                    ]
                    Mui.dialogActions[
                        Mui.button[
                            button.variant.outlined
                            prop.text "Clear All"
                            //prop.onClick (fun _ ->  CLEAR ALL checkboxes)
                        ]
                        Mui.button[
                            button.variant.outlined
                            prop.text "Apply"
                            //prop.onClick (fun _ ->  apply filter)
                        ]
                    ]
                ]
            ]
        ]