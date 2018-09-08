namespace Component

module Select =

    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Elmish
    open Fulma


    type Model = 
        { Title : string;  Items: Item List; MultiSelect : bool ; IsActive : bool }
    and Item = { Name : string; Selected : bool}


    type Msg = Select of string


    let init multi title items = 
        { 
            Title = title
            Items = 
                items
                |> List.map (fun item -> { Name = item; Selected = item = "alles" || false})
            MultiSelect = multi
            IsActive = false
        }


    let update (msg : Msg) (model : Model) =
        let select s items =
            items 
            |> List.map (fun item ->
                if item.Name = s then 
                    { item with Selected = if s = "alles" then true else not item.Selected }
                else if s <> "alles" && item.Name = "alles" then
                    { item with Selected = false }
                else if s = "alles" && item.Name <> "alles" then 
                    { item with Selected = false }
                else item )

        match msg with
        | Select s ->            
                { model with 
                    Items = 
                        if model.MultiSelect |> not then
                            model.Items
                            |> List.map (fun x -> { x with Selected = false } )
                        else model.Items
                        |> select s                        
                }



    let view (model : Model) (dispatch : Msg -> unit) = 

        let onclick cat = 
            fun _ -> 
                printfn "selecteer: %s" cat
                cat |> Select |> dispatch

        let toItem { Name = cat; Selected = sel} = 
            Dropdown.Item.a [ Dropdown.Item.IsActive sel; Dropdown.Item.Props [ OnClick (onclick cat) ] ] [ str cat ]

        let content = 
            model.Items
            |> List.map toItem
        
        Dropdown.dropdown [ Dropdown.IsHoverable; Dropdown.Props [ Style [ CSSProp.PaddingBottom "10px"  ] ] ; Dropdown.IsActive model.IsActive ]
            [ div []
                [ Button.button []
                    [ span []
                        [ str model.Title ]
                      Fulma.FontAwesome.Icon.faIcon [ Icon.Size IsSmall ] [ FontAwesome.Fa.icon FontAwesome.Fa.I.AngleDown ] ] ]
              Dropdown.menu []
                [ Dropdown.content []
                    content ]]
