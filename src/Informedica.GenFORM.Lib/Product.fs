namespace Informedica.GenForm.Lib



module Product =


    open MathNet.Numerics
    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineNoTime
    open Informedica.Utils.Lib.BCL

    open Informedica.GenUnits.Lib

    open Utils

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


    module FormRoute =


        let isSolution (mapping : FormRoute[]) form =
            mapping
            |> Array.tryFind (fun sr ->
                sr.Form |> String.equalsCapInsens form
            )
            |> Option.map _.IsSolution
            |> Option.defaultValue false


    module Reconstitution =



        let get dataUrlId : GenFormResult<_> =
            try
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

                        {
                            GPK = get "GPK"
                            Route = get "Route"
                            Department = get "Dep"
                            DiluentVolume =
                                get "DiluentVol"
                                |> toBrOpt
                                |> Option.map (ValueUnit.singleWithUnit Units.Volume.milliLiter)
                                |> Option.defaultValue (ValueUnit.singleWithUnit Units.Volume.milliLiter 1N)
                            ExpansionVolume =
                                get "ExpansionVol"
                                |> toBrOpt
                                |> Option.map (ValueUnit.singleWithUnit Units.Volume.milliLiter)
                            Diluents =
                                get "Diluents"
                                |> String.splitAt ';'
                                |> Array.map String.trim
                        }
                    )
                |> GenFormResult.createOkNoMsgs
            with
            | exn -> GenFormResult.createError "Reconstiution.get" exn


        let filter routeMapping (filter : DoseFilter) (rs : Reconstitution []) =
            let eqs a b =
                a
                |> Option.map (fun x -> x = b)
                |> Option.defaultValue true

            [|
                fun (r : Reconstitution) -> r.Route |> Mapping.eqsRoute routeMapping filter.Route
                fun (r : Reconstitution) -> r.Department |> eqs filter.Patient.Department
            |]
            |> Array.fold (fun (acc : Reconstitution[]) pred ->
                acc |> Array.filter pred
            ) rs


    module Enteral =


        let get dataUrlId unitMapping : GenFormResult<_> =
            try
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
                        let formUnit =
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
                            UseForm = false
                            UseBrand = false
                            TallMan = "" //r.TallMan
                            Synonyms = [||]
                            Product = r.Name
                            Label = r.Name
                            Form = "voeding"
                            Routes = [| "ORAAL" |]
                            FormQuantities =
                                formUnit
                                |> ValueUnit.singleWithValue 1N
                            FormUnit = formUnit
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
                                                |> Mapping.mapUnit unitMapping
                                                |> function
                                                    | None ->
                                                        writeErrorMessage $"cannot map unit: {u}"
                                                        None
                                                    | Some u ->
                                                        let u =
                                                            u
                                                            |> Units.per formUnit
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
                |> GenFormResult.createOkNoMsgs
            with
            | exn -> GenFormResult.createError "Enteral.get" exn


    module Parenteral =


        let get dataUrlId unitMapping : GenFormResult<_> =
            try
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
                            UseForm = false
                            UseBrand = false
                            TallMan = "" //r.TallMan
                            Synonyms = [||]
                            Product = r.Name
                            Label = r.Name
                            Form = "vloeistof"
                            Routes = [| "INTRAVENEUS"; "ORAAL" |]
                            FormQuantities =
                                Units.Volume.milliLiter
                                |> ValueUnit.singleWithValue 1N
                            FormUnit =
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
                                                |> Mapping.mapUnit unitMapping
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
                |> GenFormResult.createOkNoMsgs
            with
            | exn -> GenFormResult.createError "Parenteral.get" exn


    let create gen rte substs =
        {
            GPK = ""
            ATC = ""
            MainGroup = ""
            SubGroup = ""
            Generic = gen
            UseGenericName = false
            UseForm = false
            UseBrand = false
            TallMan = gen
            Synonyms = [||]
            Product = gen
            Label = gen
            Form = gen
            Routes = [| rte  |]
            FormQuantities = ValueUnit.empty
            FormUnit = NoUnit
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


    let rename defaultName useGenName (subst : Informedica.ZIndex.Lib.Types.ProductSubstance) =
        if useGenName then subst.GenericName
        else defaultName
        |> String.toLower


    let map
        unitMapping
        routeMapping
        formRoutes
        (reconstitution : Reconstitution[])
        name
        useGenName
        useForm
        useBrand
        synonyms
        formQuantities
        divisible
        molarConcentration
        (gp : Informedica.ZIndex.Lib.Types.GenericProduct)
        =

        let atc =
            gp.ATC
            |> ATCGroup.findByATC5

        let formUnit =
            gp.Substances[0].FormUnit
            |> Mapping.mapUnit unitMapping
            |> Option.defaultValue NoUnit

        let reqReconst =
            Mapping.requiresReconstitution routeMapping formRoutes (gp.Route, formUnit, gp.Form)

        let formUnit =
            if not reqReconst then formUnit
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
            UseForm = useForm
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
            Form = gp.Form |> String.toLower
            Routes = gp.Route |> Array.choose (Mapping.mapRoute routeMapping)
            FormQuantities =
                formQuantities
                |> ValueUnit.withUnit formUnit
            FormUnit = formUnit
            RequiresReconstitution = reqReconst
            Reconstitution =
                reconstitution
                |> Array.filter (fun r ->
                    r.GPK = $"{gp.Id}"
                )
            Divisible =
                match divisible with
                | Some d -> d |> BigRational.fromInt |> Some
                | None ->
                    let rs =
                        Mapping.filterFormRoutes
                            routeMapping
                            formRoutes "" (gp.Form.ToLower()) NoUnit
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
                // TODO should be group by as additional can have different concentrations
                |> Array.distinctBy _.SubstanceId
                |> Array.map (fun s ->
                    let su =
                            s.SubstanceUnit
                            |> Mapping.mapUnit unitMapping
                            |> Option.map (fun u ->
                                CombiUnit(u, OpPer, formUnit)
                            )
                            |> Option.defaultValue NoUnit
                    {
                        Name = s |> rename s.SubstanceName useGenName
                        Concentration =
                            s.SubstanceQuantity
                            |> BigRational.fromFloat
                            |> Option.map (ValueUnit.singleWithUnit su)
                        MolarConcentration =
                            if molarConcentration |> Option.isNone ||
                               s.SubstanceName |> String.equalsCapInsens name |> not then None
                            // only apply mmol to substance with the same name as the product
                            else
                                let u = Units.Molar.milliMole |> Units.per formUnit
                                molarConcentration.Value
                                |> BigRational.fromFloat
                                |> Option.map (ValueUnit.singleWithUnit u)
                    }
                )

        }


    let getFormularyProducts dataUrlId : GenFormResult<_> =
        try
            fun () ->
                Web.getDataFromSheet dataUrlId "Formulary"
                |> fun data ->
                    let getColumn =
                        data
                        |> Array.head
                        |> Csv.getStringColumn

                    data
                    |> Array.tail
                    |> Array.map (fun r ->
                        let get = getColumn r

                        {
                            GPKODE = get "GPKODE" |> Int32.parse
                            Apotheek = get "UMCU"
                            ICC = get "ICC"
                            NEO = get "NEO"
                            ICK = get "ICK"
                            HCK = get "HCK"
                            Generic = get "Generic"
                            UseGenName = get "UseGenName" = "x"
                            UseForm = get "UseForm" = "x"
                            UseBrand = get "UseBrand" = "x"
                            TallMan = get "TallMan"
                            MilliMoleOption = get "Mmol" |> Double.tryParse
                            Divisible = get "Divisible" |> Int32.tryParse
                        }
                    )
            |> StopWatch.clockFunc "retrieved formulary products"
            |> GenFormResult.createOkNoMsgs
        with
        | exn -> GenFormResult.createError "FormularyProducts" exn


    let get
        unitMapping
        routeMapping
        validForms
        formRoutes
        reconstitution
        parenteral
        enteral
        (formularyProducts: FormularyProduct[])
        =
        fun () ->
            formularyProducts
            // find the matching GenPresProducts
            |> Array.collect (fun r ->
                r.GPKODE
                |> GenPresProduct.findByGPK
                |> Array.map (fun gpp -> (r, gpp))
            )
            // collect the GenericProducts
            // filtered by "valid form" and
            // at least one substance quantity > 0
            |> Array.collect (fun (r, gpp) ->
                gpp.GenericProducts
                |> Array.filter (fun gp ->
                    gp.Id = r.GPKODE &&

                    validForms
                    |> Array.exists (String.equalsCapInsens gp.Form) &&
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

                let formQuantities =
                    gp.PrescriptionProducts
                    |> Array.map _.Quantity
                    |> Array.choose BigRational.fromFloat
                    |> Array.filter (fun br -> br > 0N)
                    |> Array.distinct
                    |> fun xs ->
                        if xs |> Array.isEmpty then [| 1N |] else xs

                gp
                |> map
                       unitMapping
                       routeMapping
                       formRoutes
                       reconstitution
                       name
                       r.UseGenName
                       r.UseForm
                       r.UseBrand
                       synonyms
                       formQuantities
                       r.Divisible
                       r.MilliMoleOption
            )
            |> Array.append parenteral
            |> Array.append enteral

        |> StopWatch.clockFunc "created products"


    /// <summary>
    /// Reconstitute the given product according to
    /// route, DoseType, department and VenousAccess location.
    /// </summary>
    /// <param name="mapping"></param>
    /// <param name="rte">The route</param>
    /// <param name="dtp">The dose type</param>
    /// <param name="dep">The department</param>
    /// <param name="loc">The venous access location</param>
    /// <param name="prod">The product</param>
    /// <returns>
    /// The reconstituted product or None if the product
    /// does not require reconstitution.
    /// </returns>
    let reconstitute mapping rte dtp dep loc (prod : Product) =
        let warnings = ResizeArray<string>()
        let eqsRoute = Mapping.eqsRoute mapping

        let prods =
            [|
                // if reconstitution is not required, the
                // original product is returned as well
                if prod.RequiresReconstitution |> not then [| prod |]
                else
                    // calculate the reconstituted products
                    prod.Reconstitution
                    |> Array.filter (fun r ->
                        (rte |> String.isNullOrWhiteSpace || r.Route |> eqsRoute (Some rte)) &&
                        dep |> Option.map (fun dep -> r.Department |> String.isNullOrWhiteSpace || r.Department |> String.equalsCapInsens dep) |> Option.defaultValue true
                    )
                    |> fun xs ->
                        if xs |> Array.isEmpty then
                            warnings.Add $"no reconstitution rules found for {prod.Generic} ({prod.Form}) with route {rte} and department {dep}"
                        xs
                    |> Array.map (fun r ->
                        let v =
                            r.ExpansionVolume
                            |> Option.map (fun v -> v + r.DiluentVolume)
                            |> Option.defaultValue r.DiluentVolume

                        { prod with
                            FormUnit =
                                Units.Volume.milliLiter
                            FormQuantities = v
                            Substances =
                                prod.Substances
                                |> Array.map (fun s ->
                                    { s with
                                        Concentration =
                                            s.Concentration
                                            |> Option.map (fun q ->
                                                // replace the old formunit with the new one
                                                let one =
                                                    Units.Volume.milliLiter
                                                    |> ValueUnit.singleWithValue 1N
                                                one * q / v
                                            )
                                    }
                                )
                        }
                    )
            |]
            |> Array.collect id

        prods,
        warnings |> Seq.distinct


    /// <summary>
    /// Filter the Product array to get all the products
    /// </summary>
    /// <param name="mapping"></param>
    /// <param name="filter">The Filter</param>
    /// <param name="prods">The array of Products</param>
    let filter mapping (filter : DoseFilter) (prods : Product []) =
        let eqsRoute = Mapping.eqsRoute mapping
        let recFilter = Reconstitution.filter mapping

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
            p.Form |> eqs filter.Form &&
            p.Routes |> Array.exists (eqsRoute filter.Route)
        )
        |> Array.map (fun p ->
            { p with
                Reconstitution =
                    p.Reconstitution
                    |> recFilter filter
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


    /// Get all pharmaceutical forms from the given Product array.
    let forms  (products : Product array) =
        products
        |> Array.map _.Form
        |> Array.distinct