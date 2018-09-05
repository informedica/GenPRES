namespace Component

open Fable.Helpers.React.ReactiveComponents
module Table =

    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Elmish
    open Fulma

    let create onClick data =
        match data with
        | h::rs ->
            let header =
                (h |> List.map (fun s -> th [] [ str s ]))
            
            let body =
                rs 
                |> List.mapi (fun i xs ->
                    let attrs =
                        if onClick |> List.isEmpty then 
                            [ OnClick (fun _ -> printfn "Table is clicked") :> IHTMLProp ]
                        else
                            let f = onClick.[i]
                            [ OnClick f :> IHTMLProp ]

                    tr attrs (xs |> List.map (fun x -> 
                                td [] [ str x]
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

