namespace Component

module Table =

    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Elmish
    open Fulma

    let create data =
        match data with
        | h::rs ->
            let header =
                (h |> List.map (fun s -> th [] [ str s ]))
            
            let body =
                rs 
                |> List.map (fun xs ->
                    tr [] (xs |> List.map (fun x -> td [] [ str x]))
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

