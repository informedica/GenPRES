namespace Components


module Select =

    open Elmish
    open Feliz
    open Feliz.UseElmish
    open Feliz.MaterialUI


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


    [<ReactComponent>]
    let View
        (input: {| label: ReactElement
                   items: 'a list
                   value: 'a option
                   handleSelect: 'a -> unit |})
        =
        let classes = useStyles ()

        let state, dispatch =
            React.useElmish (
                init input.value,
                update input.handleSelect,
                [| box input.value |]
            )

        Mui.formControl [
            prop.className classes.formControl
            formControl.margin.dense
            formControl.children [
                Mui.inputLabel [ input.label ]
                Mui.select [
                    state.Selected
                    |> Option.map string
                    |> Option.defaultValue ""
                    |> select.value

                    input.items
                    |> List.mapi (fun i item ->
                        let s = item |> string

                        Mui.menuItem [
                            prop.key i
                            prop.value s
                            prop.onClick (fun _ -> item |> Select |> dispatch)
                            menuItem.children [
                                Mui.typography [
                                    typography.variant.h6
                                    prop.text s
                                ]
                            ]
                        ]
                    )
                    |> select.children
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