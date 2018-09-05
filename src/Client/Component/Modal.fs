namespace Component

module Modal =

    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Elmish
    open Fulma


    type Model = { IsActive : bool }


    type Msg = Show of bool


    let init () = { IsActive = false }


    let update msg model =
        match msg with
        | Show b -> { model with IsActive = b }



    let cardModal title content (model : Model) (dispatch : Msg -> unit) =
        Modal.modal [ Modal.IsActive model.IsActive  ]
            [ Modal.background [ Props [ OnClick (fun _ -> false |> Show |> dispatch) ] ] [ ]
              Modal.Card.card [ ]
                [ Modal.Card.head [ ]
                    [ Modal.Card.title [ ]
                        [ str title ]
                      Delete.delete [ Delete.OnClick (fun _ -> false |> Show |> dispatch) ] [ ] ]
                  Modal.Card.body [ ]
                    [ content ]
                  Modal.Card.foot [ ]
                    [ Button.button [ Button.Color IsSuccess ]
                        [ str "OK" ] ] ] ]



