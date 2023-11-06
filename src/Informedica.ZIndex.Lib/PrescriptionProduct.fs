namespace Informedica.ZIndex.Lib


module PrescriptionProduct =

    open Informedica.Utils.Lib


    /// Create a PrescriptionProduct record
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
        Zindex.BST052T.records ()
        |> Array.filter (fun r ->
            r.MUTKOD <> 1 &&
            r.GPKODE = id
        )
        |> Array.map (fun r ->
            let p =
                Zindex.BST052T.records ()
                |> Array.find (fun r' -> r'.PRKODE = r.PRKODE)
            let nm = Names.getName p.PRNMNR Names.Full
            let lb = Names.getName p.PRNMNR Names.Label
            let un = Names.getThes r.PREENH Names.GenericUnit Names.Fifty
            let ct = Names.getThes r.THEMBT Names.PrescriptionContainer Names.Fifty
            let ps = TradeProduct.get r.PRKODE

            create r.PRKODE nm lb r.PRGALG un ct ps
        )


    /// <summary>
    /// Get all PrescriptionProducts for a given Z-Index id
    /// </summary>
    /// <remarks>
    /// This function is memoized.
    /// </remarks>
    let get : int -> PrescriptionProduct [] = Memoization.memoize _get
