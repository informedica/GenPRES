namespace Informedica.ZIndex.Lib


module GenPresProduct =

    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineNoTime

    /// Create a GenPresProduct.
    let create nm sh rt ph gps dpn unt sns =
        {
            Name = nm
            Form = sh
            Routes = rt
            PharmacologicalGroups = ph
            GenericProducts = gps
            DisplayName = dpn
            Unit = unt
            Synonyms = sns
        }


    let private parse gpks =
        let gpks =  gpks |> List.toArray

        GenericProduct.get (gpks |> Array.toList)
        |> Array.map (fun gp ->
            let n =
                gp.Substances
                |> Array.filter (fun s -> s.IsAdditional |> not)
                |> Array.map _.SubstanceName
                |> Array.distinct
                |> Array.fold (fun a s ->
                    if a = "" then s
                    else a + "/" + s) ""
            ((n, gp.Form), gp))
        |> Array.groupBy fst
        |> Array.map (fun ((nm, sh), xs) ->
            let gps = xs |> Array.map (fun (_, gp) -> gp)

            let dpn =
                Assortment.assortment ()
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
                    |> Array.map _.ATOMS)
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
                |> Array.collect _.Route
                |> Array.distinct

            create nm sh rt ph gps dpn unt [||])


    let private _get gpks =
        let useDemo = FilePath.useDemo()

        fun () ->
            if (FilePath.productCache useDemo) |> File.exists then
                FilePath.productCache useDemo
                |> Json.getCache
                |> (fun gpps ->
                    if gpks |> List.isEmpty then gpps
                    else
                        gpps
                        |> Array.filter (fun gpp ->
                            gpp.GenericProducts
                            |> Array.exists (fun gp ->
                                gpks
                                |> List.exists (fun gpk -> gp.Id = gpk)
                            )
                        )
                )
            else
                let p = FilePath.productCache useDemo
                writeInfoMessage $"No {p}, creating GenPresProduct"
                let gpps = parse gpks
                writeInfoMessage $"Created {gpps |> Array.length} GenPres Products"

                gpps |> Json.cache p
                gpps
        |> StopWatch.clockFunc "Getting GenPresProducts"


    let private memGet = Memoization.memoize _get


    /// <summary>
    /// Get all GenPresProducts for the given GenericProduct Ids.
    /// </summary>
    /// <remarks>
    /// This function is memoized
    /// </remarks>
    let get = memGet


    /// Get a list of all GenericProduct Ids.
    let getGPKS all =
        get all
        |> Array.collect (fun gpp ->
            gpp.GenericProducts
            |> Array.map _.Id
        )
        |> Array.distinct


    /// Get the string representation of a GenPresProduct.
    let toString (gpp : GenPresProduct) =
        gpp.Name + " " + gpp.Form + " " + (gpp.Routes |> String.concat "/")


    /// <summary>
    /// Filter GenPresProducts by name, pharmaceutical form and route.
    /// </summary>
    /// <param name="n">The name</param>
    /// <param name="f">The form</param>
    /// <param name="r">The route</param>
    let filter n f r =
        get []
        |> Array.filter (fun gpp ->
            (n = "" || gpp.Name   |> String.equalsCapInsens n) &&
            (f = "" || gpp.Form  |> String.equalsCapInsens f) &&
            (r = "" || gpp.Routes |> Array.exists (fun r' -> r' |> String.equalsCapInsens r))
        )


    /// Get a Map of GenericProduct Ids to GenPresProducts.
    let getGPKMap =
        fun () ->
            get []
            |> Array.collect (fun gpp ->
                gpp.GenericProducts
                |> Array.map (fun gp -> gp.Id, gpp)
            )
            |> Array.groupBy fst
            |> Array.map (fun (k, v) -> k, v |> Array.map snd)
            |> Map.ofArray
        |> Memoization.memoize


    /// Find GenPresProducts by GenericProduct Id.
    let findByGPK gpk =
        match (getGPKMap ()) |> Map.tryFind gpk with
        | Some gpps -> gpps
        | None -> [||]


    /// Load all GenPresProducts in memory.
    let load = get >> ignore


    /// Get all Routes for all GenPresProducts.
    let getRoutes =
        fun () ->
            get []
            |> Array.collect _.Routes
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize


    /// Get all pharmaceutical forms for all GenPresProducts.
    let getForms =
        fun () ->
            get []
            |> Array.map _.Form
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize


    /// Get all Units for all GenPresProducts.
    let getUnits =
        fun () ->
            get []
            |> Array.map _.Unit
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize


    /// <summary>
    /// Get all FormRoutes for all GenPresProducts.
    /// </summary>
    /// <returns>
    /// An array of tuples of Forms and Routes.
    /// </returns>
    let getFormRoutes =
        fun () ->
            get []
            |> Array.map (fun gpp ->
                gpp.Form, gpp.Routes
            )
            |> Array.groupBy fst
            |> Array.map (fun (form, routes) ->
                form,
                routes
                |> Array.collect snd
                |> Array.distinct
            )
            |> Array.distinct
            |> Array.sort


    /// <summary>
    /// Get all RouteForms for an array of GenPresProducts.
    /// </summary>
    /// <param name="gpps">The GenPresProducts</param>
    /// <returns>
    /// An array of tuples of Route and Form.
    /// </returns>
    let routeForms (gpps : GenPresProduct[]) =
        // route form
        gpps
        |> Array.collect (fun gpp ->
            gpp.Routes
            |> Array.map (fun route ->
                route,
                gpp.Form
            )
        )
        |> Array.distinct


    /// <summary>
    /// Get all FormUnits for all GenPresProducts.
    /// </summary>
    /// <returns>
    /// An array of tuples of Form and Units.
    /// </returns>
    let getFormUnits =
        fun () ->
            get []
            |> Array.map (fun gpp ->
                gpp.Form, gpp.Unit
            )
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize


    /// Get all Substance Units for all GenPresProducts.
    let getSubstanceUnits =
        fun () ->
            get []
            |> Array.collect (fun gpp ->
                gpp.GenericProducts
                |> Array.collect (fun gp ->
                    gp.Substances
                    |> Array.map _.SubstanceUnit
                )
            )
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize



    /// Get all Generic Units for all GenPresProducts.
    let getGenericUnits =
        fun () ->
            get []
            |> Array.collect (fun gpp ->
                gpp.GenericProducts
                |> Array.collect (fun gp ->
                    gp.Substances
                    |> Array.map _.GenericUnit
                )
            )
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize


    /// <summary>
    /// Get all Substance names, quantities and units
    /// all GenPresProducts that contain the given GenericProduct Id.
    /// </summary>
    /// <param name="gpk">The GenericProduct id</param>
    /// <returns>
    /// An array of tuples of Substance name, quantity and unit.
    /// </returns>
    let getSubstQtyUnit gpk =
        get []
        |> Array.collect (fun gpp ->
            gpp.GenericProducts
            |> Array.filter (fun gp -> gp.Id = gpk)
            |> Array.collect (fun gp ->
                gp.Substances
                |> Array.map (fun s ->
                    s.SubstanceName,
                    s.SubstanceQuantity,
                    s.SubstanceUnit
                )
            )
        )


    /// Get all GenericProducts for all GenPresProducts.
    let getGenericProducts () =
        get []
        |> Array.collect _.GenericProducts


    /// <summary>
    /// Find all GenPresProducts that contain the given string.
    /// </summary>
    /// <param name="n">The string</param>
    let search n =
        let contains = String.containsCapsInsens

        get []
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


    let findByBrand n =
        get []
        |> Array.filter (fun gpp ->
            gpp.GenericProducts
            |> Array.exists(fun gp ->
                gp.PrescriptionProducts
                |> Array.exists(fun pp  ->
                    pp.TradeProducts
                    |> Array.exists(fun tp ->
                        tp.Brand |> String.containsCapsInsens n
                    )
                )
            )
        )