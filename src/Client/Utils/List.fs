namespace Utils

module List =

    let findNearestMax n ns =
        match ns with
        | [] ->
            n

        | _ ->
            ns
            |> List.sort
            |> List.rev
            |> List.fold
                (fun x a ->
                    if (a - x) < (n - x) then
                        x
                    else
                        a
                )
                n


    let removeDuplicates xs =
        xs
        |> List.fold
            (fun xs x ->
                if xs |> List.exists ((=) x) then
                    xs
                else
                    [ x ] |> List.append xs
            )
            []
