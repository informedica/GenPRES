namespace Component

module Modal =

    open Fable.Import.React
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Elmish
    open Fulma


    type Msg = 
        | Close


    let cardModal title content (dispatch : Msg -> unit) =

        Modal.modal [ Modal.IsActive true  ]
            [ Modal.background [ Props [ OnClick (fun _ -> Close |> dispatch) ] ] [ ]
              Modal.Card.card [ ]
                [ Modal.Card.head [ ]
                    [ Modal.Card.title [ ]
                        [ str title ]
                      Delete.delete [ Delete.OnClick (fun _ -> Close |> dispatch) ] [ ] ]
                  Modal.Card.body [ ]
                    [ content ]
                  Modal.Card.foot [ ]
                    [ Button.button [ Button.Color IsSuccess; Button.Props [ OnClick (fun _ -> Close |> dispatch)] ]
                        [ str "OK" ] ] ] ]



