namespace Informedica.ZIndex.Lib


module TradeProduct =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL


    let create id nm lb br cm dn uw rt ps =
        {
            Id = id
            Name = nm
            Label = lb
            Brand = br
            Company = cm
            Denominator = dn
            UnitWeight = uw
            Route = rt
            ConsumerProducts = ps
        }


    let _get id =
        Zindex.BST031T.records ()
        |> Array.filter (fun r  ->
            r.MUTKOD <> 1 &&
            r.PRKODE = id
        )
        |> Array.map (fun r ->
            let nm = Names.getName r.HPNAMN Names.Full
            let lb = Names.getName r.HPNAMN Names.Label
            let ps = ConsumerProduct.get r.HPKODE

            let rt =
                Zindex.BST760T.records ()
                |> Array.filter (fun x -> x.HPKODE = r.HPKODE)
                |> Array.map (fun x -> x.ENKTDW)
                |> Array.map (fun tdw -> Names.getThes tdw Names.Route Names.TwentyFive)
                |> Array.filter String.notEmpty
                |> Array.distinct

            create r.HPKODE nm lb r.MSNAAM r.FSNAAM r.HPDEEL r.HPSGEW rt ps
        )


    let get : int -> TradeProduct [] = Memoization.memoize _get