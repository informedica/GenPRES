namespace Components

module Select =
    open Fable.Core
    open Feliz.MaterialUI
    open Feliz
    open Elmish

    type SelectItem =
        { Key : string
          Value : string }

    type Model =
        { Label : string
          Selected : SelectItem option
          Items : SelectItem list }

    let init lbl items =
        { Label = lbl
          Selected = None
          Items =
              items
              |> List.map (fun i ->
                     { Key = i
                       Value = i }) }

    type Msg = Select of string

    let updateModel s model =
        { model with Selected =
                         model.Items |> List.tryFind (fun i -> i.Value = s) }

    let update msg model =
        match msg with
        | Select s -> model |> updateModel s

    let useStyles = 
        Styles.makeStyles(fun styles theme ->
            {|
                formControl = styles.create [
                    style.minWidth "115px"
                    style.margin 10
                ]
            |}
        )

    // let private styles (theme : ITheme) : IStyles list =
    //     [ Styles.FormControl [ MinWidth "115px"
    //                            CSSProp.Margin "10px" ] ]

    let private selectItem e =
        Mui.menuItem [ 
            prop.key e.Key
            prop.value e.Value
            menuItem.children [
                Mui.typography [
                    typography.variant.h6
                    prop.text (e.Value)
                ]
            ]
        ]
        |> prop.children
            // HTMLAttr.Value e.Value
            //        Key(e.Key) ]
            // [ typography [ TypographyProp.Variant TypographyVariant.H6 ]
            //       [ e.Value |> str ] ]


    [<ReactComponent>]
    let View(input: {| model : Model; dispatch : Msg -> unit |}) =
        let classes = useStyles ()
        
        Mui.formControl [
            prop.className classes.formControl
            formControl.margin.dense
            formControl.children [
                Mui.inputLabel [
                    prop.text input.model.Label
                ]
                Mui.select [
                    input.model.Selected
                    |> Option.map (fun i -> i.Value)
                    |> Option.defaultValue ""
                    |> select.value 

                    select.onChange (fun e ->
                        e
                        |> Select
                        |> input.dispatch
                    )

                    yield!
                        input.model.Items 
                        |> List.map selectItem
                ]
            ]
        ]
        // formControl [ MaterialProp.Margin FormControlMargin.Dense
        //               Class classes?formControl ]
        //     [ inputLabel [] [ str model.Label ]
        //       select [ HTMLAttr.Value(model.Selected
        //                               |> Option.map (fun i -> i.Value)
        //                               |> Option.defaultValue "")
        //                DOMAttr.OnChange(fun ev ->
        //                    ev.Value
        //                    |> Select
        //                    |> dispatch) ] [ model.Items
        //                                     |> List.map selectItem
        //                                     |> ofList ] ]


    let render model dispatch = View({| model = model; dispatch = dispatch |})