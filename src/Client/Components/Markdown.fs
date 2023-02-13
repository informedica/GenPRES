namespace Components


module Markdown =

    open Feliz
    open Feliz.MaterialUI
    open Feliz.Markdown
    open MaterialUI.Typography
    open MaterialUI.ListItem



    let useStyles =
        Styles.makeStyles (fun styles theme ->
            {|
                section =
                    styles.create [
                        style.backgroundColor theme.palette.primary.main
                        //style.color theme.palette.common.white
                        ]
            |}

        )


    [<ReactComponent>]
    let View (props: {| text: string |}) =
        let classes = useStyles ()

        Markdown.markdown [
            markdown.children props.text
            markdown.components [
                markdown.components.h1 (fun props ->
                    MaterialTypography.create [
                        match props.level with
                        | 1 -> MaterialTypography.variant "h3"
                        | 2 -> MaterialTypography.variant "h4"
                        | 3 -> MaterialTypography.variant "h5"
                        | 4 -> MaterialTypography.variant "h6"
                        | 5 -> MaterialTypography.variant "body1"
                        | 6 -> MaterialTypography.variant "body2"
                        | _ -> ()

                        MaterialTypography.color "primary"
                        if props.level = 2 then
                            prop.className "foo" //classes.section
                        prop.style [ style.marginTop 20 ]
                        prop.children props.children
                    ]
                )

                markdown.components.table (fun props ->
                    Mui.tableContainer [
                        Mui.table props.children
                    ]
                )

                markdown.components.thead (fun props ->
                    Mui.tableHead props.children
                )

                markdown.components.tbody (fun props ->
                    Mui.tableBody props.children
                )

                markdown.components.trow (fun props ->
                    Mui.tableRow [
                        tableRow.hover true
                        tableRow.children props.children
                    ]
                )

                markdown.components.tcell (fun props ->
                    Mui.tableCell [
                        if props.isHeader then
                            prop.className classes.section
                        tableCell.children props.children
                    ]
                )

                markdown.components.ul (fun props ->
                    Mui.list [
                        list.children props.children
                    ]
                )

                markdown.components.li (fun props ->
                    Mui.listItem [
                        listItem.divider true
                        listItem.button true
                        let children =
                            MaterialTypography.create [
                                MaterialTypography.variant "body1"
                                prop.style [ style.fontWeight.bold ]
                                MaterialTypography.color "textSecondary"
                                prop.children props.children
                            ]

                        listItem.children children
                    ]
                )

                markdown.components.p (fun props ->
                    Utils.Logging.log "props" props.children

                    React.fragment [
                        // prop.style [
                        //     style.marginTop 10
                        //     style.paddingLeft 0
                        // ]
                        // let children =
                        MaterialTypography.create [
                            MaterialTypography.color "textSecondary"
                            prop.children props.children
                        ]

                        // container.children children
                        ]
                )
            ]
        ]


    let render text = View({| text = text |})