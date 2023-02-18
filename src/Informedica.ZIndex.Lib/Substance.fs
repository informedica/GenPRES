namespace Informedica.ZIndex.Lib


module Substance =

    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib


    let create id pk nm ms mr fm un ds =
        {
            Id = id
            Pk = pk
            Name = nm
            Mole = ms
            MoleReal = mr
            Formula = fm
            Unit = un
            Density = ds
        }


    let cache (sbs : Substance []) = Json.cache FilePath.substanceCache sbs


    let parse () =
        Zindex.BST750T.records ()
        |> Array.filter (fun r -> r.MUTKOD <> 1)
        |> Array.map (fun r ->
            let un =
                match r.GNVOOR |> Int32.tryParse with
                | Some i ->  Names.getThes i Names.GenericUnit Names.Fifty
                | None -> r.GNVOOR

            create r.GNGNK r.GNNKPK r.GNGNAM r.GNMOLE r.GNMOLS r.GNFORM un r.GNSGEW
        )


    let _get _ =
        if FilePath.substanceCache  |> File.exists then
            FilePath.substanceCache
            |> Json.getCache
        else
            printfn "No cache creating Substance"
            let substs = parse ()
            substs |> Json.cache FilePath.substanceCache
            substs


    let get : unit -> Substance [] =
        Memoization.memoize _get


    let load () = get () |> ignore
