namespace Informedica.Utils.Lib


[<RequireQualifiedAccess>]
module Seq =



    let prepend xs1 xs2 = xs1 |> Seq.append xs2


    let pickSeq pl (xs: 'a seq) =
        match pl with
        | [] -> xs
        | _ -> seq { for i in pl -> xs |> Seq.item i }


    let allEqual succ fail xs =
        if xs   |> Seq.length = 0 then fail
        elif xs |> Seq.length = 1 then 
            xs
            |> Seq.head
            |> succ
        else
            let x = xs |> Seq.head
            if xs |> Array.forall ((=) x) then succ x 
            else fail


    let allEqualToString xs = xs |> allEqual string ""


    let allEqualToOpt xs = xs |> allEqual Some None


    let allUnique xs =
        (xs
         |> Set.ofSeq
         |> Set.count) = (xs |> Seq.length)
