namespace Component

module Modal =

    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Elmish
    open Fulma


    type Model = { IsActive : bool; Title : string; Content: string }


    type Msg = 
        | Show of string * string
        | Close


    let init () = { IsActive = false; Title = ""; Content = "" }


    let update msg model =
        match msg with
        | Show (t, c) -> 
            { model with IsActive = true; Title = t; Content = c }
        | Close -> 
            init ()



    let cardModal (model : Model) (dispatch : Msg -> unit) =
        let title = model.Title
        let content = model.Content

        Modal.modal [ Modal.IsActive model.IsActive  ]
            [ Modal.background [ Props [ OnClick (fun _ -> Close |> dispatch) ] ] [ ]
              Modal.Card.card [ ]
                [ Modal.Card.head [ ]
                    [ Modal.Card.title [ ]
                        [ str title ]
                      Delete.delete [ Delete.OnClick (fun _ -> Close |> dispatch) ] [ ] ]
                  Modal.Card.body [ ]
                    [ str content ]
                  Modal.Card.foot [ ]
                    [ Button.button [ Button.Color IsSuccess ]
                        [ str "OK" ] ] ] ]



