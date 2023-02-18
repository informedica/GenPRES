namespace Informedica.ZIndex.Lib


module PrescriptionProduct =

    open Informedica.Utils.Lib


    let create id nm lb qt un ct ps =
        {
            Id = id
            Name = nm
            Label = lb
            Quantity = qt
            Unit = un
            Container = ct
            TradeProducts = ps
        }


    let _get id =
        Zindex.BST051T.records ()
        |> Array.filter (fun r ->
            r.MUTKOD <> 1 &&
            r.GPKODE = id &&
            Zindex.BST050T.records ()
            |> Array.exists (fun r' ->
                r'.PRKODE = r.PRKODE
            )
        )
        |> Array.map (fun r ->
            let p =
                Zindex.BST050T.records ()
                |> Array.find (fun r' -> r'.PRKODE = r.PRKODE)
            let nm = Names.getName p.PRNMNR Names.Full
            let lb = Names.getName p.PRNMNR Names.Label
            let un = Names.getThes r.XPEHHV Names.GenericUnit Names.Fifty
            let ct = Names.getThes r.HPEMBT Names.PrescriptionContainer Names.Fifty
            let ps = TradeProduct.get r.PRKODE

            create r.PRKODE nm lb r.HPGALG un ct ps
        )


    let get : int -> PrescriptionProduct [] = Memoization.memoize _get
