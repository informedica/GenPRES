namespace Informedica.GenForm.Lib




module Product =

    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL


    module GenPresProduct = Informedica.ZIndex.Lib.GenPresProduct
    module ATCGroup = Informedica.ZIndex.Lib.ATCGroup


    module Location =


        /// Get a string representation of the VenousAccess.
        let toString = function
            | PVL -> "PVL"
            | CVL -> "CVL"
            | AnyAccess -> ""


        /// Get a VenousAccess from a string.
        let fromString s =
            match s with
            | _ when s |> String.equalsCapInsens "PVL" -> PVL
            | _ when s |> String.equalsCapInsens "CVL" -> CVL
            | _ -> AnyAccess



    module ShapeRoute =


        /// <summary>
        /// Check if the given shape is a solution using
        /// a ShapeRoute array.
        /// </summary>
        /// <param name="srs">The ShapeRoute array</param>
        /// <param name="shape">The Shape</param>
        let isSolution shape  =
            Mapping.mappingShapeRoute
            |> Array.tryFind (fun sr ->
                sr.Shape |> String.equalsCapInsens shape
            )
            |> Option.map _.IsSolution
            |> Option.defaultValue false



    module Reconstitution =

        open Utils

        // GPK
        // Route
        // DoseType
        // Dep
        // CVL
        // PVL
        // DiluentVol
        // ExpansionVol
        // Diluents
        let private get_ () =
            Web.getDataFromSheet Web.dataUrlIdGenPres "Reconstitution"
            |> fun data ->

                let getColumn =
                    data
                    |> Array.head
                    |> Csv.getStringColumn

                data
                |> Array.tail
                |> Array.map (fun r ->
                    let get = getColumn r
                    let toBrOpt = BigRational.toBrs >> Array.tryHead

                    {|
                        GPK = get "GPK"
                        Route = get "Route"
                        Location =
                            match get "CVL", get "PVL" with
                            | s1, _ when s1 |> String.isNullOrWhiteSpace |> not -> CVL
                            | _, s2 when s2 |> String.isNullOrWhiteSpace |> not -> PVL
                            | _ -> AnyAccess
                        DoseType = get "DoseType" |> DoseType.fromString
                        Dep = get "Dep"
                        DiluentVol = get "DiluentVol" |> toBrOpt
                        ExpansionVol = get "ExpansionVol" |> toBrOpt
                        Diluents = get "Diluents"
                    |}
                )


        /// Get the Reconstitution array.
        /// Returns an anonymous record with the following fields:
        /// unit -> {| Dep: string; DiluentVol: BigRational option; Diluents: string; DoseType: DoseType; ExpansionVol: BigRational option; GPK: string; Location: VenousAccess; Route: string |} array
        let get = Memoization.memoize get_


        /// <summary>
        /// Filter the Reconstitution array to get all the reconstitution rules
        /// that match the given filter.
        /// </summary>
        /// <param name="filter">The Filter</param>
        /// <param name="rs">The array of reconstitution rules</param>
        let filter (filter : Filter) (rs : Reconstitution []) =
            let eqs a b =
                a
                |> Option.map (fun x -> x = b)
                |> Option.defaultValue true

            [|
                fun (r : Reconstitution) -> r.Route |> eqs filter.Route
                fun (r : Reconstitution) ->
                    if filter.Patient.VenousAccess = [AnyAccess] ||
                       filter.Patient.VenousAccess |> List.isEmpty then true
                    else
                        match filter.DoseType with
                        | AnyDoseType -> true
                        | _ -> filter.DoseType = r.DoseType
                fun (r : Reconstitution) -> r.Department |> eqs filter.Patient.Department
                fun (r : Reconstitution) ->
                    match r.Location, filter.Patient.VenousAccess with
                    | AnyAccess, _
                    | _, []
                    | _, [ AnyAccess ] -> true
                    | _ ->
                        filter.Patient.VenousAccess
                        |> List.exists ((=) r.Location)
            |]
            |> Array.fold (fun (acc : Reconstitution[]) pred ->
                acc |> Array.filter pred
            ) rs



    module Parenteral =

        open Informedica.GenUnits.Lib


        let private get_ () =
            Web.getDataFromSheet Web.dataUrlIdGenPres "ParentMeds"
            |> fun data ->
                let getColumn =
                    data
                    |> Array.head
                    |> Csv.getStringColumn

                data
                |> Array.tail
                |> Array.map (fun r ->
                    let get = getColumn r
                    let toBrOpt = BigRational.toBrs >> Array.tryHead

                    {|
                        Name = get "Name"
                        Substances =
                            [|
                                "volume mL", get "volume mL" |> toBrOpt
                                "energie kCal", get "energie kCal" |> toBrOpt
                                "eiwit g", get "eiwit g" |> toBrOpt
                                "KH g", get "KH g" |> toBrOpt
                                "vet g", get "vet g" |> toBrOpt
                                "Na mmol", get "Na mmol" |> toBrOpt
                                "K mmol", get "K mmol" |> toBrOpt
                                "Ca mmol", get "Ca mmol" |> toBrOpt
                                "P mmol", get "P mmol" |> toBrOpt
                                "Mg mmol", get "Mg mmol" |> toBrOpt
                                "Fe mmol", get "Fe mmol" |> toBrOpt
                                "VitD IE", get "VitD IE" |> toBrOpt
                                "Cl mmol", get "Cl mmol" |> toBrOpt

                            |]
                        Oplosmiddel = get "volume mL"
                        Verdunner = get "volume mL"
                    |}
                )
                |> Array.map (fun r ->
                    {
                        GPK =  r.Name
                        ATC = ""
                        MainGroup = ""
                        SubGroup = ""
                        Generic = r.Name
                        TallMan = "" //r.TallMan
                        Synonyms = [||]
                        Product = r.Name
                        Label = r.Name
                        Shape = ""
                        Routes = [||]
                        ShapeQuantities =
                            Units.Volume.milliLiter
                            |> ValueUnit.singleWithValue 1N
                        ShapeUnit =
                            Units.Volume.milliLiter
                        RequiresReconstitution = false
                        Reconstitution = [||]
                        Divisible = Some 10N
                        Substances =
                            r.Substances
                            |> Array.map (fun (s, q) ->
                                let n, u =
                                    match s |> String.split " " with
                                    | [n; u] -> n |> String.trim, u |> String.trim
                                    | _ -> failwith $"cannot parse substance {s}"
                                {
                                    Name = n
                                    Concentration =
                                        q
                                        |> Option.bind (fun q ->
                                            u
                                            |> Mapping.mapUnit
                                            |> function
                                                | None -> None
                                                | Some u ->
                                                    let u =
                                                        u
                                                        |> Units.per Units.Volume.milliLiter
                                                    q
                                                    |> ValueUnit.singleWithUnit u
                                                    |> Some
                                        )
                                    MultipleQuantity = None
                                }
                            )
                    }
                )


        /// Get the Parenterals as a Product array.
        let get : unit -> Product [] =
            Memoization.memoize get_



    open Informedica.GenUnits.Lib


    let private get_ () =
        // check if the shape is a solution
        let isSol = ShapeRoute.isSolution
        // TODO make this a configuration
        let rename (subst : Informedica.ZIndex.Lib.Types.ProductSubstance) defN =
            if subst.SubstanceName |> String.startsWithCapsInsens "AMFOTERICINE B" ||
               subst.SubstanceName |> String.startsWithCapsInsens "COFFEINE" then
                subst.GenericName
                |> String.replace "0-WATER" "BASE"
            else defN
            |> String.toLower

        fun () ->
            // first get the products from the GenPres Formulary, i.e.
            // the assortment
            Web.getDataFromSheet Web.dataUrlIdGenPres "Formulary"
            |> fun data ->
                let getColumn =
                    data
                    |> Array.head
                    |> Csv.getStringColumn

                let formulary =
                    data
                    |> Array.tail
                    |> Array.map (fun r ->
                        let get = getColumn r

                        {|
                            GPKODE = get "GPKODE" |> Int32.parse
                            Apotheek = get "UMCU"
                            ICC = get "ICC"
                            NEO = get "NEO"
                            ICK = get "ICK"
                            HCK = get "HCK"
                            tallMan = get "TallMan"
                        |}
                    )

                formulary
                // find the matching GenPresProducts
                |> Array.collect (fun r ->
                    r.GPKODE
                    |> GenPresProduct.findByGPK
                )
                // collect the GenericProducts
                |> Array.collect (fun gpp ->
                    gpp.GenericProducts
                    |> Array.map (fun gp -> gpp, gp)
                )
                // create the Product records
                |> Array.map (fun (gpp, gp) ->
                    let atc =
                        gp.ATC
                        |> ATCGroup.findByATC5

                    let shpUnit =
                        gp.Substances[0].ShapeUnit
                        |> Mapping.mapUnit
                        |> Option.defaultValue NoUnit

                    let reqReconst = Mapping.requiresReconstitution (gp.Route, shpUnit, gp.Shape)
                    let shpUnit =
                        if not reqReconst then shpUnit
                        else
                            Units.Volume.milliLiter
                    {
                        GPK =  $"{gp.Id}"
                        ATC = gp.ATC |> String.trim
                        MainGroup =
                            atc
                            |> Array.map _.AnatomicalGroup
                            |> Array.tryHead
                            |> Option.defaultValue ""
                        SubGroup =
                            atc
                            |> Array.map _.TherapeuticSubGroup
                            |> Array.tryHead
                            |> Option.defaultValue ""
                        Generic =
                            rename gp.Substances[0] gpp.Name
                        TallMan =
                            match formulary |> Array.tryFind(fun f -> f.GPKODE = gp.Id) with
                            | Some p when p.tallMan |> String.notEmpty -> p.tallMan
                            | _ -> ""
                        Synonyms =
                            gpp.GenericProducts
                            |> Array.collect (fun gp ->
                                gp.PrescriptionProducts
                                |> Array.collect (fun pp ->
                                    pp.TradeProducts
                                    |> Array.map (_.Brand)
                                )
                            )
                            |> Array.distinct
                            |> Array.filter String.notEmpty
                        Product =
                            gp.PrescriptionProducts
                            |> Array.collect (fun pp ->
                                pp.TradeProducts
                                |> Array.map _.Label
                            )
                            |> Array.distinct
                            |> function
                            | [| p |] -> p
                            | _ -> ""
                        Label = gp.Label
                        Shape = gp.Shape |> String.toLower
                        Routes = gp.Route |> Array.choose Mapping.mapRoute
                        ShapeQuantities =
                            gpp.GenericProducts
                            |> Array.collect (fun gp ->
                                gp.PrescriptionProducts
                                |> Array.map _.Quantity
                                |> Array.choose BigRational.fromFloat
                            )
                            |> Array.filter (fun br -> br > 0N)
                            |> Array.distinct
                            |> fun xs ->
                                if xs |> Array.isEmpty then [| 1N |] else xs
                                |> ValueUnit.withUnit shpUnit
                        ShapeUnit = shpUnit
                        RequiresReconstitution = reqReconst
                        Reconstitution =
                            if not reqReconst then [||]
                            else
                                Reconstitution.get ()
                                |> Array.filter (fun r ->
                                    r.GPK = $"{gp.Id}" &&
                                    r.DiluentVol |> Option.isSome
                                )
                                |> Array.map (fun r ->
                                    {
                                        Route = r.Route
                                        DoseType = r.DoseType
                                        Department = r.Dep
                                        Location = r.Location
                                        DiluentVolume =
                                            r.DiluentVol.Value
                                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                                        ExpansionVolume =
                                            r.ExpansionVol
                                            |> Option.map (fun v ->
                                                v
                                                |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                                            )
                                        Diluents =
                                            r.Diluents
                                            |> String.splitAt ';'
                                            |> Array.map String.trim
                                    }
                                )
                        Divisible =
                            let rs = Mapping.filterRouteShapeUnit "" (gp.Shape.ToLower()) shpUnit
                            if rs |> Array.distinct |> Array.length <> 1 then None
                            else
                                rs[0].Divisibility
                        Substances =
                            gp.Substances
                            |> Array.map (fun s ->
                                let su =
                                    s.SubstanceUnit
                                    |> Mapping.mapUnit
                                    |> Option.map (fun u ->
                                        u |> ValueUnit.per shpUnit
                                    )
                                    |> Option.defaultValue NoUnit
                                {
                                    Name = rename s s.SubstanceName
                                    Concentration =
                                        s.SubstanceQuantity
                                        |> BigRational.fromFloat
                                        |> Option.map (fun q ->
                                            q
                                            |> ValueUnit.singleWithUnit su
                                        )
                                    MultipleQuantity = None
                                }
                            )
                    }
                )
        |> StopWatch.clockFunc "created products"


    /// <summary>
    /// Get the Product array.
    /// </summary>
    /// <remarks>
    /// This function is memoized.
    /// </remarks>
    let get : unit -> Product [] =
        Memoization.memoize get_


    /// <summary>
    /// Reconstitute the given product according to
    /// route, DoseType, department and VenousAccess location.
    /// </summary>
    /// <param name="rte">The route</param>
    /// <param name="dtp">The dose type</param>
    /// <param name="dep">The department</param>
    /// <param name="loc">The venous access location</param>
    /// <param name="prod">The product</param>
    /// <returns>
    /// The reconstituted product or None if the product
    /// does not require reconstitution.
    /// </returns>
    let reconstitute rte dtp dep loc (prod : Product) =
        if prod.RequiresReconstitution |> not then None
        else
            prod.Reconstitution
            |> Array.filter (fun r ->
                (rte |> String.isNullOrWhiteSpace || r.Route |> String.equalsCapInsens rte) &&
                (r.DoseType = AnyDoseType || r.DoseType = dtp) &&
                (dep |> Option.map (fun dep -> r.Department |> String.equalsCapInsens dep) |> Option.defaultValue true) &&
                (loc |> List.isEmpty || loc |> List.exists ((=) r.Location) || r.Location = AnyAccess)
            )
            |> Array.map (fun r ->
                { prod with
                    ShapeUnit =
                        Units.Volume.milliLiter
                    ShapeQuantities = r.DiluentVolume
                    Substances =
                        prod.Substances
                        |> Array.map (fun s ->
                            { s with
                                Concentration =
                                    s.Concentration
                                    |> Option.map (fun q ->
                                        // replace the old shapeunit with the new one
                                        let one =
                                            Units.Volume.milliLiter
                                            |> ValueUnit.singleWithValue 1N
                                        (one * q) / r.DiluentVolume
                                    )
                            }
                        )
                }
            )
            |> function
            | [| p |] -> Some p
            | _       -> None


    /// <summary>
    /// Filter the Product array to get all the products
    /// </summary>
    /// <param name="filter">The Filter</param>
    /// <param name="prods">The array of Products</param>
    let filter (filter : Filter) (prods : Product []) =
        let repl s =
            s
            |> String.replace "/" ""
            |> String.replace "+" ""

        let eqs s1 s2 =
            match s1, s2 with
            | Some s1, s2 ->
                let s1 = s1 |> repl
                let s2 = s2 |> repl
                s1 |> String.equalsCapInsens s2
            | _ -> false

        prods
        |> Array.filter (fun p ->
            p.Generic |> eqs filter.Generic &&
            p.Shape |> eqs filter.Shape &&
            p.Routes |> Array.exists (eqs filter.Route)
        )
        |> Array.map (fun p ->
            { p with
                Reconstitution =
                    p.Reconstitution
                    |> Reconstitution.filter filter
            }
        )


    /// Get all Generics from the given Product array.
    let generics (products : Product array) =
        products
        |> Array.map _.Generic
        |> Array.distinct


    /// Get all Synonyms from the given Product array.
    let synonyms (products : Product array) =
        products
        |> Array.collect _.Synonyms
        |> Array.append (generics products)
        |> Array.distinct


    /// Get all Shapes from the given Product array.
    let shapes  (products : Product array) =
        products
        |> Array.map _.Shape
        |> Array.distinct


