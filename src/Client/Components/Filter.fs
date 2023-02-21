namespace Components


module Filter =

    open Feliz
    open MaterialUI5
    open FSharp.Core

    let selected = [||]

    [<ReactComponent>]
    let view(items : Map<string, string list>) : ReactElement =

        Mui.formControl [
            prop.style[ style.minWidth "115px"]
            formControl.children[
                Mui.inputLabel[
                    prop.text "Filter"
                ]
                Mui.select[
                    select.multiple true
                    select.value selected
                    select.children[
                        for key in items |> Map.keys do
                            Mui.listSubheader[
                                prop.text key
                            ]
                            for value in items |> Map.find key do
                                Mui.menuItem[
                                    prop.text value
                                    prop.onClick (fun item ->)
                                ]
                    ]
                ]
            ]
        ]

