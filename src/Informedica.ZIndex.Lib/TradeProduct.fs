namespace Informedica.ZIndex.Lib


module TradeProduct =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL


    /// Creates a ProductSubstance
    let createSubstance si so sn sq su gi gn gq gu un ia =
        {
            SubstanceId = si
            SortOrder = so
            SubstanceName = sn
            SubstanceQuantity = sq
            SubstanceUnit = su
            GenericId = gi
            GenericName = gn
            GenericQuantity = gq
            GenericUnit = gu
            ShapeUnit = un
            IsAdditional = ia
        }


    /// Creates a new TradeProduct
    let create id nm lb br cm dn uw rt ss ps =
        {
            Id = id
            Name = nm
            Label = lb
            Brand = br
            Company = cm
            Denominator = dn
            UnitWeight = uw
            Route = rt
            Substances = ss
            ConsumerProducts = ps
        }


    /// Get the Substances for a GenericProduct
    let getSubstances (hpk: Zindex.BST031T.BST031T) =
        let un = Names.getThes hpk.XSEENH Names.ShapeUnit Names.Fifty

        Zindex.BST701T.records ()
        |> Array.filter (fun ig ->
            ig.HPKODE = hpk.HPKODE &&
            ig.MUTKOD <> 1
        )
        |> Array.map (fun ig ->
            let stam =
                Zindex.BST750T.records ()
                |> Array.find (fun s -> s.GNGNK = ig.GNSTAM)

            let gn =
                Zindex.BST750T.records ()
                |> Array.find (fun s -> s.GNGNK = ig.GNGNK)

            let isAdditional = ig.GNMWHS = "H"

            let un1 = Names.getThes ig.XNMINE Names.GenericUnit Names.Fifty
            createSubstance ig.GNSTAM ig.GNVOLG stam.GNGNAM ig.GNMINH un1 ig.GNGNK gn.GNGNAM 0. "" un isAdditional
        )
        |> Array.distinct
        |> Array.sortBy _.SortOrder


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

            let ss = r |> getSubstances

            let rt =
                Zindex.BST760T.records ()
                |> Array.filter (fun x -> x.HPKODE = r.HPKODE)
                |> Array.map _.ENKTDW
                |> Array.map (fun tdw -> Names.getThes tdw Names.Route Names.TwentyFive)
                |> Array.filter String.notEmpty
                |> Array.distinct

            create r.HPKODE nm lb r.MSNAAM r.FSNAAM r.HPDEEL r.HPSGEW rt ss ps
        )


    /// <summary>
    /// Gets the TradeProduct with the specified id
    /// </summary>
    /// <remarks>
    /// This function is memoized
    /// </remarks>
    let get : int -> TradeProduct [] = Memoization.memoize _get