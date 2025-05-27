namespace Informedica.GenForm.Lib




module Product =

    open MathNet.Numerics
    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineNoTime
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
            let dataUrlId = Web.getDataUrlIdGenPres ()
            Web.getDataFromSheet dataUrlId "Reconstitution"
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
        let filter (filter : DoseFilter) (rs : Reconstitution []) =
            let eqs a b =
                a
                |> Option.map (fun x -> x = b)
                |> Option.defaultValue true

            [|
                fun (r : Reconstitution) -> r.Route |> Mapping.eqsRoute filter.Route
                fun (r : Reconstitution) -> r.Department |> eqs filter.Patient.Department
            |]
            |> Array.fold (fun (acc : Reconstitution[]) pred ->
                acc |> Array.filter pred
            ) rs


    module Enteral =

        open Informedica.GenUnits.Lib


        let private get_ () =
            let dataUrlId = Web.getDataUrlIdGenPres ()
            Web.getDataFromSheet dataUrlId "EntFeeding"

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
                        Unit = get "Eenheid"
                        Substances =
                            [|
                                "volume mL", get "volume mL" |> toBrOpt
                                "energie kCal", get "energie kCal" |> toBrOpt
                                "eiwit g", get "eiwit g" |> toBrOpt
                                "koolhydraat g", get "KH g" |> toBrOpt
                                "vet g", get "vet g" |> toBrOpt
                                "natrium mmol", get "Na mmol" |> toBrOpt
                                "kalium mmol", get "K mmol" |> toBrOpt
                                "calcium mmol", get "Ca mmol" |> toBrOpt
                                "fosfaat mmol", get "P mmol" |> toBrOpt
                                "magnesium mmol", get "Mg mmol" |> toBrOpt
                                "ijzer mmol", get "Fe mmol" |> toBrOpt
                                "VitD IE", get "VitD IE" |> toBrOpt
                                "chloor mmol", get "Cl mmol" |> toBrOpt

                            |]
                    |}
                )
                |> Array.map (fun r ->
                    let shapeUnit =
                        r.Unit
                        |> Units.fromString
                        |> Option.defaultValue Units.Volume.milliLiter
                    {
                        GPK =  r.Name
                        ATC = ""
                        MainGroup = ""
                        SubGroup = ""
                        Generic = r.Name
                        UseGenericName = false
                        UseShape = false
                        UseBrand = false
                        TallMan = "" //r.TallMan
                        Synonyms = [||]
                        Product = r.Name
                        Label = r.Name
                        Shape = "voeding"
                        Routes = [| "ORAAL" |]
                        ShapeQuantities =
                            shapeUnit
                            |> ValueUnit.singleWithValue 1N
                        ShapeUnit = shapeUnit
                        RequiresReconstitution = false
                        Reconstitution = [||]
                        Divisible = Some 10N
                        Substances =
                            r.Substances
                            |> Array.filter (fun (n, q) ->
                                n |> String.equalsCapInsens "volume mL" |> not &&
                                q
                                |> Option.map (fun br -> br > 0N)
                                |> Option.defaultValue false
                            )
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
                                                | None ->
                                                    writeErrorMessage $"cannot map unit: {u}"
                                                    None
                                                | Some u ->
                                                    let u =
                                                        u
                                                        |> Units.per shapeUnit
                                                    q
                                                    |> ValueUnit.singleWithUnit u
                                                    |> Some
                                        )
                                    MolarConcentration = None
                                }
                            )
                            |> Array.filter (fun s ->
                                s.Name |> String.notEmpty &&
                                (s.Concentration |> Option.isSome ||
                                s.MolarConcentration |> Option.isSome)
                            )
                    }
                )


        /// Get the Enteral feeding as a Product array.
        let get : unit -> Product [] =
            Memoization.memoize get_



    module Parenteral =

        open Informedica.GenUnits.Lib


        let private get_ () =
            let dataUrlId = Web.getDataUrlIdGenPres ()
            Web.getDataFromSheet dataUrlId "ParentMeds"
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
                        Name = get "Name"
                        Substances =
                            [|
                                "volume mL", get "volume mL" |> toBrOpt
                                "glucose g", get "glucose g" |> toBrOpt
                                "energie kCal", get "energie kCal" |> toBrOpt
                                "eiwit g", get "eiwit g" |> toBrOpt
                                "koolhydraat g", get "koolhydraat g" |> toBrOpt
                                "vet g", get "vet g" |> toBrOpt
                                "natrium mmol", get "natrium mmol" |> toBrOpt
                                "kalium mmol", get "kalium mmol" |> toBrOpt
                                "calcium mmol", get "calcium mmol" |> toBrOpt
                                "fosfaat mmol", get "fosfaat mmol" |> toBrOpt
                                "magnesium mmol", get "magnesium mmol" |> toBrOpt
                                "ijzer mmol", get "ijzer mmol" |> toBrOpt
                                "VitD IE", get "VitD IE" |> toBrOpt
                                "chloor mmol", get "chloor mmol" |> toBrOpt

                            |]
                        Oplosmiddel = get "volume mL"
                        Verdunner = get "volume mL"
                        IsOplosmiddel = get "Oplosmiddel" = "TRUE"
                        IsVerdunner = get "Verdunner" = "TRUE"
                    |}
                )
                |> Array.map (fun r ->

                    {
                        GPK =  r.GPK
                        ATC = ""
                        MainGroup = ""
                        SubGroup = ""
                        Generic = r.Name
                        UseGenericName = false
                        UseShape = false
                        UseBrand = false
                        TallMan = "" //r.TallMan
                        Synonyms = [||]
                        Product = r.Name
                        Label = r.Name
                        Shape = "vloeistof"
                        Routes = [| "INTRAVENEUS"; "ORAAL" |]
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
                            |> Array.filter (fun (n, q) ->
                                n |> String.equalsCapInsens "volume mL" |> not &&
                                q
                                |> Option.map (fun br -> br > 0N)
                                |> Option.defaultValue false
                            )
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
                                                | None ->
                                                    writeErrorMessage $"cannot map unit: {u}"
                                                    None
                                                | Some u ->
                                                    let u =
                                                        u
                                                        |> Units.per Units.Volume.milliLiter
                                                    q
                                                    |> ValueUnit.singleWithUnit u
                                                    |> Some
                                        )
                                    MolarConcentration = None
                                }
                            )
                            |> Array.filter (fun s ->
                                s.Name |> String.notEmpty &&
                                (s.Concentration |> Option.isSome ||
                                s.MolarConcentration |> Option.isSome)
                            )
                    }
                )


        /// Get the Parenterals as a Product array.
        let get : unit -> Product [] =
            Memoization.memoize get_



    open Informedica.GenUnits.Lib


    let create gen rte substs =
        {
            GPK = ""
            ATC = ""
            MainGroup = ""
            SubGroup = ""
            Generic = gen
            UseGenericName = false
            UseShape = false
            UseBrand = false
            TallMan = gen
            Synonyms = [||]
            Product = gen
            Label = gen
            Shape = gen
            Routes = [| rte  |]
            ShapeQuantities = ValueUnit.empty
            ShapeUnit = NoUnit
            RequiresReconstitution = false
            Reconstitution = [||]
            Divisible = None
            Substances =
                substs
                |> Array.map (fun s ->
                    {
                        Name = s
                        Concentration = None
                        MolarConcentration = None
                    }
                )
        }


    let rename defN useGenName (subst : Informedica.ZIndex.Lib.Types.ProductSubstance) =
        if useGenName then subst.GenericName
        else defN
        |> String.toLower


    let map
        name
        useGenName
        useShape
        useBrand
        synonyms
        shapeQuantities
        divisible
        mmol
        (gp : Informedica.ZIndex.Lib.Types.GenericProduct)
        =

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
            Generic = name
            UseGenericName = useGenName
            UseShape = useShape
            UseBrand = useBrand
            TallMan = ""
            Synonyms = synonyms
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
                shapeQuantities
                |> ValueUnit.withUnit shpUnit
            ShapeUnit = shpUnit
            RequiresReconstitution = reqReconst
            Reconstitution =
                Reconstitution.get ()
                |> Array.filter (fun r ->
                    r.GPK = $"{gp.Id}" &&
                    r.DiluentVol |> Option.isSome
                )
                |> Array.map (fun r ->
                    {
                        Route = r.Route
                        Department = r.Dep
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
                match divisible with
                | Some d -> d |> BigRational.fromInt |> Some
                | None ->
                    let rs = Mapping.filterRouteShapeUnit "" (gp.Shape.ToLower()) NoUnit
                    if rs |> Array.length = 0 then None
                    else
                        rs[0].Divisibility
            Substances =
                let additional =
                    gp.PrescriptionProducts
                    |> Array.collect (fun pp ->
                        pp.TradeProducts
                        |> Array.collect _.Substances
                    )
                    |> Array.filter (fun s ->
                        s.SubstanceQuantity > 0. &&
                        s.IsAdditional
                    )

                gp.Substances
                |> Array.filter (fun ps ->
                    ps.SubstanceQuantity > 0.
                )
                |> Array.append additional
                |> Array.distinctBy _.SubstanceId
                |> Array.map (fun s ->
                    let su =
                            s.SubstanceUnit
                            |> Mapping.mapUnit
                            |> Option.map (fun u ->
                                CombiUnit(u, OpPer, shpUnit)
                            )
                            |> Option.defaultValue NoUnit
                    {
                        Name = s |> rename s.SubstanceName useGenName
                        Concentration =
                            s.SubstanceQuantity
                            |> BigRational.fromFloat
                            |> Option.map (ValueUnit.singleWithUnit su)
                        MolarConcentration =
                            if mmol |> Option.isNone ||
                               s.SubstanceName |> String.equalsCapInsens name |> not then None
                            // only apply mmol to substance with the same name as the product
                            else
                                let u = Units.Molar.milliMole |> Units.per shpUnit
                                mmol.Value
                                |> BigRational.fromFloat
                                |> Option.map (ValueUnit.singleWithUnit u)
                    }
                )

        }


    let private get_ () =
        fun () ->
            // first get the products from the GenPres Formulary, i.e.
            // the assortment
            let dataUrlId = Web.getDataUrlIdGenPres ()
            Web.getDataFromSheet dataUrlId "Formulary"
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
                            Generic = get "Generic"
                            useGenName = get "UseGenName" = "x"
                            useShape = get "UseShape" = "x"
                            useBrand = get "UseBrand" = "x"
                            tallMan = get "TallMan"
                            mmol = get "Mmol" |> Double.tryParse
                            divisible = get "Divisible" |> Int32.tryParse
                        |}
                    )

                formulary
                // find the matching GenPresProducts
                |> Array.collect (fun r ->
                    r.GPKODE
                    |> GenPresProduct.findByGPK
                    |> Array.map (fun gpp -> (r, gpp))
                )
                // collect the GenericProducts
                // filtered by "valid shape" and
                // at least one substance quantity > 0
                |> Array.collect (fun (r, gpp) ->
                    gpp.GenericProducts
                    |> Array.filter (fun gp ->
                        gp.Id = r.GPKODE &&
                        Mapping.validShapes ()
                        |> Array.exists (String.equalsCapInsens gp.Shape) &&
                        gp.Substances
                        |> Array.exists (fun s ->
                            s.SubstanceQuantity > 0.
                        )
                    )
                    |> Array.map (fun gp -> r, gp)
                )
                // create the Product records
                |> Array.map (fun (r, gp) ->
                    let name = r.Generic |> String.toLower

                    let synonyms =
                        gp.PrescriptionProducts
                        |> Array.collect (fun pp ->
                            pp.TradeProducts
                            |> Array.map _.Brand
                        )
                        |> Array.distinct
                        |> Array.filter String.notEmpty

                    let shapeQuantities =
                        gp.PrescriptionProducts
                        |> Array.map _.Quantity
                        |> Array.choose BigRational.fromFloat
                        |> Array.filter (fun br -> br > 0N)
                        |> Array.distinct
                        |> fun xs ->
                            if xs |> Array.isEmpty then [| 1N |] else xs

                    gp
                    |> map name
                           r.useGenName
                           r.useShape
                           r.useBrand
                           synonyms
                           shapeQuantities
                           r.divisible
                           r.mmol
                )
                |> Array.append (Parenteral.get ())
                |> Array.append (Enteral.get ())

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
        [|
            // if reconstitution is not required, the
            // original product is returned as well
            if prod.RequiresReconstitution |> not then [| prod |]
            // calculate the reconstituted products
            prod.Reconstitution
            |> Array.filter (fun r ->
                (rte |> String.isNullOrWhiteSpace || r.Route |> Mapping.eqsRoute (Some rte)) &&
                (dep |> Option.map (fun dep -> r.Department |> String.isNullOrWhiteSpace || r.Department |> String.equalsCapInsens dep) |> Option.defaultValue true)
            )
            |> Array.map (fun r ->
                let v =
                    r.ExpansionVolume
                    |> Option.map (fun v -> v + r.DiluentVolume)
                    |> Option.defaultValue r.DiluentVolume

                { prod with
                    ShapeUnit =
                        Units.Volume.milliLiter
                    ShapeQuantities = v
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
                                        (one * q) / v
                                    )
                            }
                        )
                }
            )
        |]
        |> Array.collect id


    /// <summary>
    /// Filter the Product array to get all the products
    /// </summary>
    /// <param name="filter">The Filter</param>
    /// <param name="prods">The array of Products</param>
    let filter (filter : DoseFilter) (prods : Product []) =
        let repl s =
            s
            |> String.replace "/" ""
            |> String.replace "+" ""
            |> String.replace "(" ""
            |> String.replace ")" ""
            |> String.trim

        let eqs s1 s2 =
            match s1, s2 with
            | Some s1, s2 ->
                let s1 = s1 |> repl
                let s2 = s2 |> repl
                s1 |> String.equalsCapInsens s2
            | _ -> true

        prods
        |> Array.filter (fun p ->
            p.Generic |> eqs filter.Generic &&
            p.Shape |> eqs filter.Shape &&
            p.Routes |> Array.exists (Mapping.eqsRoute filter.Route)
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