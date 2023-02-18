namespace Informedica.ZIndex.Lib


module GenPresProduct =

    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib


    let create nm sh rt ph gps dpn unt sns =
        {
            Name = nm
            Shape = sh
            Routes = rt
            PharmacologicalGroups = ph
            GenericProducts = gps
            DisplayName = dpn
            Unit = unt
            Synonyms = sns
        }


    let private parse (prs : Assortment []) =
        let gpks =  prs |> Array.map (fun pr -> pr.GPK)

        GenericProduct.get (gpks |> Array.toList)
        |> Array.map (fun gp ->
            let n =
                gp.Substances
                |> Array.map (fun s -> s.SubstanceName)
                |> Array.distinct
                |> Array.fold (fun a s ->
                    if a = "" then s
                    else a + "/" + s) ""
            ((n, gp.Shape), gp))
        |> Array.groupBy (fun (key, _) -> key)
        |> Array.map (fun ((nm, sh), xs) ->
            let gps = xs |> Array.map (fun (_, gp) -> gp)

            let dpn =
                prs
                |> Array.filter (fun pr ->
                        gps
                        |> Array.exists (fun gp ->
                            pr.GPK = gp.Id
                        )

                )
                |> Array.fold (fun acc pr ->
                    if acc = "" then pr.Generic
                    else acc
                ) ""

            let ph =
                gps
                |> Array.collect (fun gp ->
                    Zindex.BST801T.records ()
                    |> Array.filter (fun atc ->
                        atc.ATCODE
                        |> String.trim
                        |> String.equalsCapInsens (gp.ATC |> String.subString 0 5)
                    )
                    |> Array.map (fun atc -> atc.ATOMS))
                |> Array.distinct

            let unt =
                gps
                |> Array.fold (fun acc gp ->
                    if acc <> "" then acc
                    else
                        gp.PrescriptionProducts
                        |> Array.fold (fun acc pp ->
                            if acc <> "" then acc
                            else  pp.Unit
                        ) ""
                ) ""

            let rt =
                gps
                |> Array.collect (fun gp ->
                    gp.Route
                )
                |> Array.distinct

            create nm sh rt ph gps dpn unt [||])


    let private _get (prs : Assortment []) =
        if FilePath.productCache |> File.exists then
            FilePath.productCache
            |> Json.getCache
            |> (fun gpps ->
                if prs |> Array.isEmpty then gpps
                else
                    gpps
                    |> Array.filter (fun gpp ->
                        gpp.GenericProducts
                        |> Array.exists (fun gp ->
                            prs
                            |> Array.exists (fun pr -> pr.GPK = gp.Id)
                        )
                    )
            )
        else
            printfn "No cache creating GenPresProduct"
            let gsps = parse prs
            gsps |> Json.cache FilePath.productCache
            gsps


    let private memGet = Memoization.memoize _get


    let private getAssortment () =
        let gpks =
            Assortment.assortment ()
            |> Array.map (fun pr -> pr.GPK)

        Array.empty
        |> memGet
        |> Array.filter (fun pr ->
            gpks
            |> Array.exists (fun gpk -> pr.GenericProducts |> Array.exists (fun gp -> gp.Id = gpk))
        )


    let private getAll () =
        Array.empty |> memGet


    let get all = if all then getAll () else getAssortment ()


    let getGPKS all =
        get all
        |> Array.collect (fun gpp ->
            gpp.GenericProducts
            |> Array.map (fun gp -> gp.Id)
        )
        |> Array.distinct


    let toString (gpp : GenPresProduct) =
        gpp.Name + " " + gpp.Shape + " " + (gpp.Routes |> String.concat "/")


    let filter all n s r =
        if all then getAll () else getAssortment ()
        |> Array.filter (fun gpp ->
            (n = "" || gpp.Name   |> String.equalsCapInsens n) &&
            (s = "" || gpp.Shape  |> String.equalsCapInsens s) &&
            (r = "" || gpp.Routes |> Array.exists (fun r' -> r' |> String.equalsCapInsens r))
        )


    let findByGPK gpk =
        getAll ()
        |> Array.filter (fun gpp ->
            gpp.GenericProducts
            |> Array.exists (fun gp -> gp.Id = gpk)
        )


    let load all =
        if all then getAll () else getAssortment ()
        |> ignore


    let getRoutes =
        fun () ->
            getAll ()
            |> Array.collect (fun gpp ->
                gpp.Routes
            )
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize


    let getShapes =
        fun () ->
            getAll ()
            |> Array.map (fun gpp ->
                gpp.Shape
            )
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize


    let getUnits =
        fun () ->
            getAll ()
            |> Array.map (fun gpp ->
                gpp.Unit
            )
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize


    let getShapeRoutes =
        fun () ->
            getAssortment ()
            |> Array.map (fun gpp ->
                gpp.Shape, gpp.Routes
            )
            |> Array.groupBy fst
            |> Array.map (fun (k, vs) ->
                k,
                vs
                |> Array.collect snd
                |> Array.distinct
            )
            |> Array.distinct
            |> Array.sort


    let getShapeUnits =
        fun () ->
            getAssortment ()
            |> Array.map (fun gpp ->
                gpp.Shape, gpp.Unit
            )
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize


    let getSubstanceUnits =
        fun () ->
            getAll ()
            |> Array.collect (fun gpp ->
                gpp.GenericProducts
                |> Array.collect (fun gp ->
                    gp.Substances
                    |> Array.map (fun s -> s.SubstanceUnit)
                )
            )
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize



    let getGenericUnits =
        fun () ->
            getAll ()
            |> Array.collect (fun gpp ->
                gpp.GenericProducts
                |> Array.collect (fun gp ->
                    gp.Substances
                    |> Array.map (fun s -> s.GenericUnit)
                )
            )
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize


    let getSubstQtyUnit gpk =
        getAll ()
        |> Array.collect (fun gpp ->
            gpp.GenericProducts
            |> Array.filter (fun gp -> gp.Id = gpk)
            |> Array.collect (fun gp ->
                gp.Substances
                |> Array.map (fun s ->
                    s.SubstanceName, s.SubstanceQuantity, s.SubstanceUnit
                )
            )
        )


    let getGenericProducts () =
        get true
        |> Array.collect (fun gpp -> gpp.GenericProducts)


    // Find a product
    let search n =
        let contains = String.containsCapsInsens

        get true
        |> Array.filter (fun gpp ->
            gpp.Name |> contains n ||
            gpp.GenericProducts
            |> Array.exists (fun gp ->
                gp.Name |> contains n ||
                gp.PrescriptionProducts
                |> Array.exists (fun pp ->
                    pp.TradeProducts
                    |> Array.exists (fun tp ->
                        tp.Label
                        |> contains n
                    )
                )
            )
        )


    let routeShapes (gpps : GenPresProduct[]) =
        // route shape
        gpps
        |> Array.collect (fun gpp ->
            gpp.Routes
            |> Array.map (fun r ->
                r,
                gpp.Shape
            )
        )
        |> Array.distinct
