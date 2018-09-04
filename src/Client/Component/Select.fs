namespace Component

module Select =

    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Elmish
    open Fulma


    type Model = Item List
    and Item = { Name : string; Selected : bool}


    type Msg = Select of string


    let init items = 
        items
        |> List.map (fun item -> { Name = item; Selected = item = "alles" || false})


    let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =

        match msg with
        | Select s ->
            printfn "update with select: %s" s
            
            let newModel = 
                currentModel 
                |> List.map (fun item ->
                    if item.Name = s then 
                        { item with Selected = if s = "alles" then true else not item.Selected }
                    else if s <> "alles" && item.Name = "alles" then
                        { item with Selected = false }
                    else if s = "alles" && item.Name <> "alles" then 
                        { item with Selected = false }
                    else item
                )

            newModel, Cmd.none


    let view (model : Model) (dispatch : Msg -> unit) = 

        let onclick cat = 
            fun e -> 
                printfn "selecteer: %s, %A" cat e
                cat |> Select |> dispatch

        let toItem { Name = cat; Selected = sel} = 
            Dropdown.Item.a [ Dropdown.Item.IsActive sel; Dropdown.Item.Props [ OnClick (onclick cat) ] ] [ str cat ]

        let content = 
            model
            |> List.map toItem
        
        Dropdown.dropdown [ Dropdown.IsHoverable; Dropdown.Props [ Style [ CSSProp.PaddingBottom "10px"  ] ] ]
            [ div []
                [ Button.button []
                    [ span []
                        [ str "Selecteer" ]
                      Fulma.FontAwesome.Icon.faIcon [ Icon.Size IsSmall ] [ FontAwesome.Fa.icon FontAwesome.Fa.I.AngleDown ]  ]]
              Dropdown.menu []
                [ Dropdown.content []
                    content ]]
