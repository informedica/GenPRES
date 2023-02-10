namespace Components


module Select =

    open Elmish
    open Feliz
    open Feliz.UseElmish
    open MaterialUI.FormControl
    open MaterialUI.InputLabel
    open MaterialUI.Select
    open MaterialUI.MenuItem
    open MaterialUI.Typography


    type State<'a> = { Selected: 'a Option }


    let init value : State<_> * Cmd<_> = { Selected = value }, Cmd.none


    type Msg<'a> = Select of 'a

    // the update function is intitiated with a handle to
    // report back to the parent which item is selected.
    let update handleSelect msg state =
        match msg with
        | Select s ->
            { state with Selected = s |> Some },
            Cmd.ofSub (fun _ -> s |> handleSelect)

    //TODO Fix Styles
    // let useStyles =
    //     Styles.makeStyles (fun styles theme ->
    //         {|
    //             formControl =
    //                 styles.create [
    //                     style.minWidth "115px"
    //                     style.margin 10
    //                 ]
    //         |}
    //     )


    [<ReactComponent>]
    let View
        (input: {| label: ReactElement
                   items: 'a list
                   value: 'a option
                   handleSelect: 'a -> unit |})
        =
        //let classes = useStyles ()

        let state, dispatch =
            React.useElmish (
                init input.value,
                update input.handleSelect,
                [| box input.value |]
            )

        let defaultVal =
            match input.items with
            | [one] -> one |> string
            | _ -> ""

        MaterialFormControl.create [
            //prop.className classes.formControl
            MaterialFormControl.margin "dense"
            prop.children [
                MaterialInputLabel.create [
                    prop.children[
                        input.label
                    ]
                ]
                MaterialSelect.create [
                    state.Selected
                    |> Option.map string
                    |> Option.defaultValue defaultVal
                    |> prop.value

                    input.items
                    |> List.mapi (fun i item ->
                        let s = item |> string

                        MaterialMenuItem.create [
                            prop.key i
                            prop.value s
                            prop.onClick (fun _ -> item |> Select |> dispatch)
                            prop.children [
                                MaterialTypography.create [
                                    MaterialTypography.variant "h6"
                                    prop.text s
                                ]
                            ]
                        ]
                    )
                    |> prop.children
                ]
            ]
        ]

    // render the select with a label (a ReactElement)
    // the items to select from, a value that should be
    // selected (None of no value is selected) and
    // a handle that gives back the selected element.
    let render label items value handleSelect =
        View(
            {|
                label = label
                items = items
                value = value
                handleSelect = handleSelect
            |}
        )