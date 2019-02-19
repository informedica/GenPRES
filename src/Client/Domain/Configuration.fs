namespace Domain

module Configuration =
    open GenPres.Shared.Types.Configuration

    let calculateSelects dep config =
        match config |> List.tryFind (fun s -> s.Department = dep) with
        | Some set ->
            [ set.MinAge .. set.MaxAge / 12 ], [ 0..11 ],
            [ set.MinWeight .. 0.1 .. 9.9 ]
            @ [ 10.0..1.0..19.0 ] @ [ 20.0..5.0..set.MaxWeight ], [ 50..200 ]
        | None -> [], [], [], []
