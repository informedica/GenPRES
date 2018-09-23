namespace Component

module Navbar =


    open Fulma
    open Fable.Import.React
    open Fable.Helpers.React
    open Fable.Helpers.React.Props

    type Config =
        {
            Brand : ReactElement
            StartMenu : MenuItem list
            EndMenu : MenuItem list
        }
    and MenuItem =
        | Divider
        | Item of Item
    and Item =
        {
            Icon : Fulma.FontAwesome.Fa.I.IFontAwesomeIcon Option
            Name : string
            Handler : MouseEvent -> unit
        }


    let menuItem (i : FontAwesome.Fa.I.FontAwesomeIcons option) n h =
        let icon =
            match i with
            | Some i' -> i' :> FontAwesome.Fa.I.IFontAwesomeIcon |> Some
            | None -> None

        { Icon = icon; Name = n; Handler = h } |> Item


    let divider = Divider


    let config brand startm endm =
        {
            Brand = brand
            StartMenu = startm
            EndMenu = endm
        }


    type Model = 
        { 
            IsBurgerActive : bool
        }

    
    let init () = { IsBurgerActive = false }


    type Msg = ToggleBurger


    let update msg (model : Model) =
        match msg with
        | ToggleBurger ->
            { model with IsBurgerActive = not model.IsBurgerActive }


    let navbarView dispatch (config : Config) (model : Model) =

        let iconModWhite = Icon.Modifiers [ Modifier.TextColor IsWhite ]

        let faIcon isWhite icon =
            if isWhite then
                Fulma.FontAwesome.Icon.faIcon 
                    [ Icon.Size IsSmall; iconModWhite ] 
                    [ FontAwesome.Fa.icon icon ]
            else
                Fulma.FontAwesome.Icon.faIcon 
                    [ Icon.Size IsSmall ] 
                    [ FontAwesome.Fa.icon icon ]

        let burgerIcon = faIcon true FontAwesome.Fa.I.Bars

        let burger =
            Navbar.burger 
                [ Fulma.Common.CustomClass (if model.IsBurgerActive then "is-active" else "")
                  Fulma.Common.Props [ OnClick (fun _ -> dispatch ToggleBurger) ] ]
                [ span [] []
                  span [] []
                  span [] [] ]

        // burger is the last child of brand
        let brand = Navbar.Brand.div [] [ config.Brand; burger ]

        let createMenu isEnd items =
            let ddr = if isEnd then [ Navbar.Dropdown.IsRight ] else []

            let itm i n = 
                let inl : IHTMLProp list = [ Style [ CSSProp.Display "inline-block"; CSSProp.PaddingLeft "10px" ] ]

                match i with
                | Some icon ->
                    div [] 
                        [ div inl [(icon |> faIcon false)]; div inl [ str n ] ]
                | None ->
                    div [] 
                        [ div inl [ str n ] ]

            let ml =
                items
                |> List.map (fun sm ->
                    match sm with
                    | Divider ->
                        Navbar.divider [] []
                    | Item item -> 
                        Navbar.Item.a [ Navbar.Item.Props [ OnClick item.Handler ] ]
                            [ itm item.Icon item.Name ]
                )
            
            Navbar.Item.div  [ Navbar.Item.HasDropdown 
                               Navbar.Item.IsHoverable ]
                // Clicking this link div on an ipad doesn't do anything                            
                [ Navbar.Link.div [ ]
                    [ burgerIcon ]      
                
                  Navbar.Dropdown.div ddr ml ]

        let startMenu config = 
            Navbar.Start.div [] [ (config.StartMenu |> createMenu false) ]

        let endMenu config = 
            Navbar.End.div [] [ (config.EndMenu |> createMenu true) ]

        let menu =
            []
            // Only add start menu if there are menu items
            |> (fun nb ->
                if config.StartMenu |> List.isEmpty then nb
                else 
                    [ (config |> startMenu) ]
                    |> List.append nb
            )
            // Only add end menu if there are menu items
            |> (fun nb ->
                if config.EndMenu |> List.isEmpty then nb
                else 
                    [ (config |> endMenu) ]
                    |> List.append nb
            )
            |> Navbar.menu [ Navbar.Menu.IsActive model.IsBurgerActive ]

        [ brand; menu ]
        |> Navbar.navbar
            [ Navbar.Color Color.IsDark
              Navbar.Modifiers [ Modifier.TextColor IsWhite ]
              Navbar.Props [ Style [ CSSProp.Padding "10px" ] ]
              Navbar.HasShadow ]


        