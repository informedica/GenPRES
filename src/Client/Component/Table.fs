namespace Component

open Fable.Helpers.React.ReactiveComponents
module Table =

    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Elmish
    open Fulma


    let createMobileData data =
        match data with
        | _::tail ->
            tail
            |> List.map (fun row ->
                let d =
                    row
                    |> List.fold (fun a c ->
                        [ div [] [ c ] ]
                        |> List.append a
                    ) []
                [ div [] d ]
            )
        | _ -> data            


    let create isMobile onClick data =
        match data with
        | h::rs ->
            let header =
                if isMobile then []
                else
                    (h |> List.map (fun el -> th [] [ el ]))
            
            let body =
                let rs = if isMobile then data |> createMobileData else rs
                rs 
                |> List.mapi (fun i xs ->
                    let attrs =
                        if onClick |> List.isEmpty then 
                            [ OnClick (fun _ -> printfn "Table is clicked") :> IHTMLProp ]
                        else
                            let f = onClick.[i]
                            [ OnClick f :> IHTMLProp ]

                    tr attrs (xs |> List.map (fun el -> 
                                td [] [ el ]
                            )                                                    
                        )
                )

            header, body

        | _ -> [], []
        |> (fun (h, b) ->
            Table.table [ Table.IsBordered
                          Table.IsFullWidth
                          Table.IsStriped
                          Table.IsHoverable]
                        [ thead [] h; tbody [] b ]
        )

