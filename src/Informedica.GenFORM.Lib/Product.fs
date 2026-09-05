namespace Informedica.GenForm.Lib


module Product =


    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineNoTime
    open Informedica.GenUnits.Lib

    open Utils

    module GenPresProduct = Informedica.ZIndex.Lib.GenPresProduct
    module ATCGroup = Informedica.ZIndex.Lib.ATCGroup


    let parseFormularyProducts (data: string[][]) : Result<FormularyProduct[], Message list> =
        try
            let getColumn = data |> Array.head |> Csv.getStringColumn

            let products =
                data
                |> Array.tail
                |> Array.map (fun r ->
                    let get = getColumn r

                    // Extract departments from the columns marked with 'x'
                    let departments =
                        [ "UMCU"; "ICC"; "NEO"; "ICK"; "HCK" ]
                        |> List.filter (fun dept -> (get dept) = "x")

                    // Determine product type - this is a placeholder, actual logic needed
                    let getProdType s =
                        match s with
                        | "medication" -> ProductType.MedicationProduct
                        | "enteral" -> ProductType.EnteralProduct
                        | "parenteral" -> ProductType.ParenteralProduct
                        | _ -> ProductType.NoProduct

                    // TODO: the sheet still carries "UseForm" and "UseBrand"
                    // columns that nothing reads any more (the naming flags were
                    // dropped from FormularyProduct). Confirm with the sheet owner
                    // and remove the columns, or start honouring them again.
                    {
                        GPK = get "GPKODE"
                        ProductType = get "Type" |> getProdType
                        Departments = departments
                        Generic = get "Generic"
                        TallMan = get "TallMan"
                        Divisible = get "Divisible" |> Int32.tryParse
                        UseGenName = get "UseGenName" = "x"
                        Mmol = get "Mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        Form = get "Form" |> Option.ofObj
                        Brand = get "Brand" |> Option.ofObj
                        GenName = get "GenName" |> Option.ofObj
                        GStandName = get "GStand" |> Option.ofObj
                        Unit = get "Unit" |> Option.ofObj
                        EnergyKCal = get "Energy kCal" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        CarbG = get "Carb g" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        ProtG = get "Prot g" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        LipG = get "Lip g" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        SodMmol = get "Sod mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        PotMmol = get "Pot mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        CalcMmol = get "Calc mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        PosphMmol = get "Posph mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        MagnMmol = get "Magn mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        ChlorMmol = get "Chlor mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        IronMmol = get "Iron mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        VitDIE = get "VitD IE" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        IsReconste = get "IsReconste" = "x"
                        IsDilute = get "IsDilute" = "x"
                        IsAdditive = get "IsAdditive" = "x"
                    }
                )
                // filter out dummy
                |> Array.filter (fun fp -> fp.GPK = "0" |> not)

            Ok products
        with exn ->
            Result.createError "getFormularyProducts" exn


    let getFormularyProducts dataUrlId =
        Web.GoogleSheets.getCsvDataFromSheetSync dataUrlId "Formulary"
        |> Result.mapError (fun err -> [ err |> Warning ])
        |> Result.bind parseFormularyProducts


    let getAdditionalSubstances (fp: FormularyProduct) =
        [

            "glucose g", fp.CarbG
            "energie kCal", fp.EnergyKCal
            "eiwit g", fp.ProtG
            "koolhydraat g", fp.CarbG
            "vet g", fp.LipG
            "natrium mmol", fp.SodMmol
            "kalium mmol", fp.PotMmol
            "calcium mmol", fp.CalcMmol
            "fosfaat mmol", fp.PosphMmol
            "magnesium mmol", fp.MagnMmol
            "ijzer mmol", fp.IronMmol
            "VitD IE", fp.VitDIE
            "chloor mmol", fp.ChlorMmol
        ]


    module Location =


        /// Get a string representation of the VenousAccess.
        let toString =
            function
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


        let isSolution (mapping: FormRoute[]) form =
            mapping
            |> Array.tryFind (fun sr -> sr.Form |> String.equalsCapInsens form)
            |> Option.map _.IsSolution
            |> Option.defaultValue false


    module Reconstitution =


        let parseReconstitution (data: string[][]) : Result<_, _> =
            try
                data
                |> fun data ->

                    let getColumn = data |> Array.head |> Csv.getStringColumn

                    data
                    |> Array.tail
                    |> Array.map (fun r ->
                        let get = getColumn r
                        let toBrOpt = BigRational.toBrs >> Array.tryHead

                        {
                            GPK = get "GPK"
                            Route = get "Route"
                            Location = get "Loc" |> fun s -> if s |> String.isNullOrWhiteSpace then None else Some s
                            Department = get "Dep" |> fun s -> if s |> String.isNullOrWhiteSpace then None else Some s
                            DiluentVolume =
                                get "DiluentVol"
                                |> toBrOpt
                                |> Option.map (ValueUnit.singleWithUnit Units.Volume.milliLiter)
                                |> Option.defaultValue (ValueUnit.singleWithUnit Units.Volume.milliLiter 1N)
                            ExpansionVolume =
                                get "ExpansionVol"
                                |> toBrOpt
                                |> Option.map (ValueUnit.singleWithUnit Units.Volume.milliLiter)
                            Diluents = get "Diluents" |> String.splitAt ';' |> Array.map String.trim
                        }
                    )
                |> Ok
            with exn ->
                Result.createError "Reconstiution.get" exn


        let get dataUrlId : Result<_, _> =
            Web.getDataFromSheet dataUrlId "Reconstitution" |> parseReconstitution


        let filter routeMapping (filter: DoseFilter) (rs: Reconstitution[]) =
            let eqsOpt a b =
                match a, b with
                | Some a, Some b -> a = b
                | _ -> true

            [|
                // should match the route if filter.Route is given
                fun (r: Reconstitution) -> r.Route |> Mapping.eqsRoute routeMapping filter.Route
                // if both filter and rule have location, they must match; if either is None, pass
                fun (r: Reconstitution) -> r.Location |> eqsOpt filter.Patient.Location
                // if both filter and rule have department, they must match; if either is None, pass
                fun (r: Reconstitution) -> r.Department |> eqsOpt filter.Patient.Department
            |]
            |> Array.fold (fun (acc: Reconstitution[]) pred -> acc |> Array.filter pred) rs


    let createSubstance name conc unit formUnit unitMapping =
        match conc with
        | Some br when br > 0N ->
            let conc =
                unit
                |> Mapping.mapUnit unitMapping
                |> function
                    | None ->
                        writeErrorMessage $"cannot map unit: {unit}"
                        None
                    | Some u ->
                        let isMolar = u |> ValueUnit.Group.eqsGroup Units.Molar.milliMole
                        let u = u |> ValueUnit.per formUnit

                        (isMolar, br |> ValueUnit.singleWithUnit u) |> Some

            {
                Name = name
                Concentration = conc |> Option.map snd
                MolarConcentration = conc |> Option.bind (fun (isMolar, vu) -> if isMolar then vu |> Some else None)
            }
            |> Some
        | _ -> None


    module Enteral =


        let createProduct name formUnit unitMapping substs =
            {
                GPK = name
                ATC = ""
                MainGroup = ""
                SubGroup = ""
                Generic = name
                TallMan = "" //r.TallMan
                Synonyms = [||]
                ProductLabels = [ name ]
                Label = name
                Form = "voeding"
                Routes = [| "ORAAL" |]
                FormQuantities = formUnit |> ValueUnit.singleWithValue 1N
                FormUnit = formUnit
                RequiresReconstitution = false
                Reconstitution = [||]
                Divisible = Some 10N
                Substances =
                    substs
                    |> List.choose (fun (s, q) ->
                        let n, u =
                            match s |> String.split " " with
                            | [ n; u ] -> n |> String.trim, u |> String.trim
                            | _ -> raise (System.FormatException $"cannot parse substance {s}")

                        createSubstance n q u formUnit unitMapping
                    )
                    |> List.filter (fun s ->
                        s.Name |> String.notEmpty
                        && (s.Concentration |> Option.isSome || s.MolarConcentration |> Option.isSome)
                    )
                    |> List.toArray
                TradeProducts = []
            }


        let get unitMapping (prods: FormularyProduct[]) =
            prods
            |> Array.filter _.ProductType.IsEnteralProduct
            |> Array.choose (fun fp ->
                let formUnit = fp.Unit |> Option.bind UnitsParse.fromString

                match formUnit with
                | None -> None
                | Some fu -> fp |> getAdditionalSubstances |> createProduct fp.Generic fu unitMapping |> Some
            )


    module Parenteral =

        let createProduct name unitMapping substs =
            {
                GPK = name
                ATC = ""
                MainGroup = ""
                SubGroup = ""
                Generic = name
                TallMan = "" //r.TallMan
                Synonyms = [||]
                ProductLabels = [ name ]
                Label = name
                Form = "vloeistof"
                Routes = [| "INTRAVENEUS"; "ORAAL" |]
                FormQuantities = Units.Volume.milliLiter |> ValueUnit.singleWithValue 1N
                FormUnit = Units.Volume.milliLiter
                RequiresReconstitution = false
                Reconstitution = [||]
                Divisible = Some 10N
                Substances =
                    substs
                    |> List.choose (fun (s, q) ->
                        let n, u =
                            match s |> String.split " " with
                            | [ n; u ] -> n |> String.trim, u |> String.trim
                            | _ -> raise (System.FormatException $"cannot parse substance {s}")

                        createSubstance n q u Units.Volume.milliLiter unitMapping
                    )
                    |> List.filter (fun s ->
                        s.Name |> String.notEmpty
                        && (s.Concentration |> Option.isSome || s.MolarConcentration |> Option.isSome)
                    )
                    |> List.toArray
                    |> Array.filter (fun s ->
                        s.Name |> String.notEmpty
                        && (s.Concentration |> Option.isSome || s.MolarConcentration |> Option.isSome)
                    )
                TradeProducts = []
            }

        let get unitMapping (prods: FormularyProduct[]) =
            prods
            |> Array.filter _.ProductType.IsParenteralProduct
            |> Array.map (fun fp -> fp |> getAdditionalSubstances |> createProduct fp.Generic unitMapping)


    let create gen frm rte substs =
        {
            GPK = ""
            ATC = ""
            MainGroup = ""
            SubGroup = ""
            Generic = gen
            TallMan = gen
            Synonyms = [||]
            ProductLabels = [ gen ]
            Label = gen
            Form = frm
            Routes = [| rte |]
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
            TradeProducts = []
        }


    let rename defaultName useGenName (subst: Informedica.ZIndex.Lib.Types.ProductSubstance) =
        if useGenName then subst.GenericName else defaultName
        |> String.toLower


    let map
        unitMapping
        routeMapping
        formRoutes
        (reconstitution: Reconstitution[])
        name
        synonyms
        formQuantities
        (fpOpt: FormularyProduct option)
        (gp: Informedica.ZIndex.Lib.Types.GenericProduct)
        =

        let atc = gp.ATC |> ATCGroup.findByATC5

        let formUnit =
            gp.Substances[0].FormUnit
            |> Mapping.mapUnit unitMapping
            |> Option.defaultValue NoUnit

        let reqReconst =
            Mapping.requiresReconstitution routeMapping formRoutes (gp.Route, formUnit, gp.Form)

        let formUnit = if not reqReconst then formUnit else Units.Volume.milliLiter

        let mapSubst (s: Informedica.ZIndex.Lib.Types.ProductSubstance) =
            let su =
                s.SubstanceUnit
                |> Mapping.mapUnit unitMapping
                |> Option.map (fun u -> CombiUnit(u, OpPer, formUnit))
                |> Option.defaultValue NoUnit

            {
                Name =
                    s
                    |> rename s.SubstanceName (fpOpt |> Option.map _.UseGenName |> Option.defaultValue false)
                Concentration =
                    s.SubstanceQuantity
                    |> BigRational.fromFloat
                    |> Option.map (ValueUnit.singleWithUnit su)
                MolarConcentration =
                    match fpOpt with
                    | Some fp ->
                        if
                            fp.Mmol |> Option.isNone
                            || s.SubstanceName |> String.equalsCapInsens name |> not
                        then
                            None
                        // only apply mmol to substance with the same name as the product
                        else
                            let u = Units.Molar.milliMole |> ValueUnit.per formUnit
                            fp.Mmol |> Option.map (ValueUnit.singleWithUnit u)
                    | None -> None
            }

        {
            GPK = $"{gp.Id}"
            ATC = gp.ATC |> String.trim
            MainGroup = atc |> Array.map _.AnatomicalGroup |> Array.tryHead |> Option.defaultValue ""
            SubGroup =
                atc
                |> Array.map _.TherapeuticSubGroup
                |> Array.tryHead
                |> Option.defaultValue ""
            Generic = name
            TallMan = ""
            Synonyms = synonyms
            ProductLabels =
                gp.PrescriptionProducts
                |> Array.collect (fun pp -> pp.TradeProducts |> Array.map _.Label)
                |> Array.distinct
                |> Array.toList
            Label = gp.Label
            Form = gp.Form |> String.toLower
            Routes = gp.Route |> Array.choose (Mapping.mapRoute routeMapping)
            FormQuantities = formQuantities |> ValueUnit.withUnit formUnit
            FormUnit = formUnit
            RequiresReconstitution = reqReconst
            Reconstitution = reconstitution |> Array.filter (fun r -> r.GPK = $"{gp.Id}")
            Divisible =
                match fpOpt |> Option.map _.Divisible |> Option.defaultValue None with
                | Some d -> d |> BigRational.fromInt |> Some
                | None ->
                    let rs =
                        Mapping.filterFormRoutes routeMapping formRoutes "" (gp.Form.ToLower()) NoUnit

                    if rs |> Array.length = 0 then None else rs[0].Divisibility
            Substances =
                let ppAdditional =
                    gp.PrescriptionProducts
                    |> Array.collect (fun pp -> pp.TradeProducts |> Array.collect _.Substances)
                    |> Array.filter (fun s -> s.SubstanceQuantity > 0. && s.IsAdditional)

                let fpAdditional =
                    match fpOpt with
                    | Some fp ->

                        fp
                        |> getAdditionalSubstances
                        |> List.choose (fun (s, q) ->
                            let n, u =
                                match s |> String.split " " with
                                | [ n; u ] -> n |> String.trim, u |> String.trim
                                | _ -> raise (System.FormatException $"cannot parse substance {s}")

                            createSubstance n q u formUnit unitMapping
                        )
                        |> List.filter (fun s ->
                            s.Name |> String.notEmpty
                            && (s.Concentration |> Option.isSome || s.MolarConcentration |> Option.isSome)
                        )
                        |> List.toArray
                    | None -> [||]

                gp.Substances
                |> Array.filter (fun ps -> ps.SubstanceQuantity > 0.)
                |> Array.append ppAdditional
                |> Array.distinctBy _.SubstanceId
                |> Array.map mapSubst
                |> Array.append fpAdditional
            TradeProducts =
                gp.PrescriptionProducts
                |> Array.collect _.TradeProducts
                |> Array.map (fun tp ->
                    {
                        HPK = tp.Id |> string //|> Hpk
                        Brand = tp.Brand
                        Substances = tp.Substances |> Array.map mapSubst |> Array.toList

                    }
                )
                |> Array.toList
        }


    /// <summary>
    /// Pure: build the ProductComponent array from already-loaded ZIndex
    /// GenPresProducts. Performs no IO so it is directly testable; the ZIndex
    /// read is hoisted into the impure shell (see <c>get</c>).
    /// </summary>
    let fromGenPresProducts
        unitMapping
        routeMapping
        validForms
        formRoutes
        reconstitution
        parenteral
        enteral
        (formularyProducts: FormularyProduct[])
        (genPresProducts: Informedica.ZIndex.Lib.Types.GenPresProduct[])
        : ProductComponent[]
        =
        // build a GPK -> FormularyProduct lookup once, so matching is
        // O(1) per GenericProduct instead of O(formularyProducts).
        let formByGpk =
            formularyProducts
            |> Array.choose (fun fp -> fp.GPK |> Int32.tryParse |> Option.map (fun gpk -> gpk, fp))
            |> Map.ofArray

        // start from ALL GenPresProducts (not just formulary GPKs), collect the
        // GenericProducts filtered by "valid form" and at least one substance
        // quantity > 0, then match each with a formulary product by GPK (None if
        // absent, so the generic product is still included). This prep is cheap.
        let pairs =
            genPresProducts
            |> Array.collect (fun gpp ->
                gpp.GenericProducts
                |> Array.filter (fun gp ->
                    validForms |> Array.exists (String.equalsCapInsens gp.Form)
                    && gp.Substances |> Array.exists (fun s -> s.SubstanceQuantity > 0.)
                )
            )
            |> Array.map (fun gp -> (formByGpk |> Map.tryFind gp.Id), gp)

        // Build one Product record per (formulary, generic-product) pair.
        let buildProduct (fp: FormularyProduct option, gp: Informedica.ZIndex.Lib.Types.GenericProduct) =
            // The Generic is the canonical match key: the official GStand generic
            // name (falling back to the formulary Generic, then the ZIndex name).
            // Any brand/form suffix the formulary adds is kept OUT of the match
            // key so dose-rule selection by component name still matches, and is
            // surfaced for display via Label instead.
            let name =
                fp
                |> Option.map (fun fp -> fp.GStandName |> Option.defaultValue fp.Generic |> String.toLower)
                |> Option.defaultValue gp.Name

            // Display label: when the formulary Generic carries a brand/form
            // suffix (i.e. differs from the GStand match key) keep it as the
            // human-facing label; otherwise fall back to the ZIndex label.
            let displayLabel =
                match fp with
                | Some fp when fp.Generic |> String.toLower <> name -> fp.Generic
                | _ -> gp.Label

            let synonyms =
                gp.PrescriptionProducts
                |> Array.collect (fun pp -> pp.TradeProducts |> Array.map _.Brand)
                |> Array.distinct
                |> Array.filter String.notEmpty

            let formQuantities =
                gp.PrescriptionProducts
                |> Array.map _.Quantity
                |> Array.choose BigRational.fromFloat
                |> Array.filter (fun br -> br > 0N)
                |> Array.distinct
                |> fun xs -> if xs |> Array.isEmpty then [| 1N |] else xs

            gp
            |> map unitMapping routeMapping formRoutes reconstitution name synonyms formQuantities fp
            |> fun prod -> { prod with Label = displayLabel }

        // Build the products in parallel: buildProduct is pure but heavy
        // (~99% of this function). Warm a single chunk single-threaded first to
        // force the one-time (static) initialization of the unit modules on one
        // thread, avoiding a cold concurrent type-initialization deadlock when the
        // parallel tasks below trigger those inits at once.
        let warm, tail =
            if pairs |> Array.length <= Parallel.totalWorders then
                pairs, [||]
            else
                pairs[.. Parallel.totalWorders - 1], pairs[Parallel.totalWorders ..]

        let head = warm |> Array.map buildProduct

        tail
        |> Array.chunkBySize Parallel.totalWorders
        |> Array.map (fun chunk -> async { return chunk |> Array.map buildProduct })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.collect id
        |> Array.append head
        |> Array.append parenteral
        |> Array.append enteral


    /// <summary>
    /// Impure adapter: reads the ZIndex GenPresProducts and delegates to the
    /// pure <c>fromGenPresProducts</c>. Kept for existing callers/tests.
    /// </summary>
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
            GenPresProduct.get []
            |> fromGenPresProducts
                unitMapping
                routeMapping
                validForms
                formRoutes
                reconstitution
                parenteral
                enteral
                formularyProducts
        |> StopWatch.clockFunc "created products"


    /// <summary>
    /// Pure: keep only the ZIndex GenericProducts referenced by dose-rule data,
    /// applying "ID overrides name": if a row has GPKs match by gp.Id; else if it
    /// has HPKs match by a TradeProduct id; else match the component name (== the
    /// would-be ProductComponent.Generic) plus route. Returns GenPresProducts with
    /// their GenericProducts narrowed (empties dropped). Built as a superset of the
    /// per-group selection done later by DoseRule.addProducts, so the resulting
    /// dose-rule output is unchanged while far fewer ProductComponents are built.
    /// </summary>
    let filterGenPresProductsByData
        routeMapping
        (formularyProducts: FormularyProduct[])
        (data: DoseRuleData[])
        (genPresProducts: Informedica.ZIndex.Lib.Types.GenPresProduct[])
        : Informedica.ZIndex.Lib.Types.GenPresProduct[]
        =

        let formByGpk =
            formularyProducts
            |> Array.choose (fun fp -> fp.GPK |> Int32.tryParse |> Option.map (fun g -> g, fp))
            |> Map.ofArray

        // same normalization Product.filter uses for name comparison
        let canon (s: string) =
            s
            |> String.replace "/" ""
            |> String.replace "+" ""
            |> String.replace "(" ""
            |> String.replace ")" ""
            |> String.trim
            |> String.toLower

        // indices built once from the data (ID overrides name per row)
        let gpkSet = System.Collections.Generic.HashSet<string>()
        let hpkSet = System.Collections.Generic.HashSet<string>()
        // name rows: canonical route (via mapRoute) -> set of canonical component names
        let nameByRoute =
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>()

        for d in data do
            if d.Generic.GPKs |> Array.isEmpty |> not then
                d.Generic.GPKs |> Array.iter (gpkSet.Add >> ignore)
            elif d.Generic.HPKs |> Array.isEmpty |> not then
                d.Generic.HPKs |> Array.iter (hpkSet.Add >> ignore)
            else
                let cmp = d.ScheduleData.DoseLimitData.Component

                let cmp =
                    if cmp |> String.isNullOrWhiteSpace then
                        d.Generic.Name
                    else
                        cmp

                match d.Route |> Mapping.mapRoute routeMapping with
                | Some r ->
                    let set =
                        match nameByRoute.TryGetValue r with
                        | true, s -> s
                        | _ ->
                            let s = System.Collections.Generic.HashSet<string>()
                            nameByRoute[r] <- s
                            s

                    set.Add(canon cmp) |> ignore
                | None -> ()

        // single pass: keep each GenericProduct if any criterion matches
        let keepGp (gp: Informedica.ZIndex.Lib.Types.GenericProduct) =
            gpkSet.Contains(string gp.Id)
            || (gp.PrescriptionProducts
                |> Array.collect _.TradeProducts
                |> Array.exists (fun tp -> hpkSet.Contains(string tp.Id)))
            || (let name =
                    formByGpk
                    |> Map.tryFind gp.Id
                    |> Option.map (fun fp ->
                        match fp.GStandName with
                        | Some n -> n
                        | None -> fp.Generic
                        |> String.toLower
                    )
                    |> Option.defaultValue gp.Name
                    |> canon

                gp.Route
                |> Array.choose (Mapping.mapRoute routeMapping)
                |> Array.exists (fun r ->
                    match nameByRoute.TryGetValue r with
                    | true, s -> s.Contains name
                    | _ -> false
                ))

        genPresProducts
        |> Array.choose (fun gpp ->
            match gpp.GenericProducts |> Array.filter keepGp with
            | [||] -> None
            | kept -> Some { gpp with GenericProducts = kept }
        )


    /// <summary>
    /// Reconstitute the given product according to
    /// route, DoseType, department and VenousAccess location.
    /// </summary>
    /// <param name="mapping"></param>
    /// <param name="dep">The department</param>
    /// <param name="loc">The venous access location</param>
    /// <param name="rte">The route</param>
    /// <param name="prod">The product</param>
    /// <returns>
    /// The reconstituted product or None if the product
    /// does not require reconstitution.
    /// </returns>
    let reconstitute mapping loc dep rte (prod: ProductComponent) =
        let warnings = ResizeArray<string>()
        let eqsRoute = Mapping.eqsRoute mapping

        let eqsOpt a b =
            match a, b with
            | Some a, Some b -> a = b
            | _ -> true

        let prods =
            [|
                // if reconstitution is not required, the
                // original product is returned as well
                if prod.RequiresReconstitution |> not then
                    [| prod |]
                else
                    // calculate the reconstituted products
                    prod.Reconstitution
                    |> Array.filter (fun r ->
                        // return true if route is not given or matches
                        (rte |> String.isNullOrWhiteSpace || r.Route |> eqsRoute (Some rte))
                        &&
                        // return true if either department is None, or both match
                        r.Department |> eqsOpt dep
                        &&
                        // return true if either location is None, or both match
                        r.Location |> eqsOpt loc
                    )
                    |> fun xs ->
                        if xs |> Array.isEmpty then
                            warnings.Add
                                $"no reconstitution rules found for {prod.Generic} ({prod.Form}) with route {rte} and department {dep}"

                        xs
                    |> Array.map (fun r ->
                        let vol =
                            r.ExpansionVolume
                            |> Option.map (fun v -> v + r.DiluentVolume)
                            |> Option.defaultValue r.DiluentVolume

                        { prod with
                            FormUnit = Units.Volume.milliLiter
                            FormQuantities = vol
                            Substances =
                                prod.Substances
                                |> Array.map (fun s ->
                                    { s with
                                        Concentration =
                                            s.Concentration
                                            |> Option.map (fun q ->
                                                let one = Units.Volume.milliLiter |> ValueUnit.singleWithValue 1N
                                                one * q / vol
                                            )
                                    }
                                )
                        }
                    )
            |]
            |> Array.collect id

        prods, warnings |> Seq.distinct


    let filterOutTradeProducts brand ids (prods: ProductComponent[]) =
        if ids |> Array.isEmpty && brand |> String.isNullOrWhiteSpace then
            prods
        else
            prods
            |> Array.map (fun p ->
                { p with
                    TradeProducts =
                        p.TradeProducts
                        |> List.filter (fun tp ->
                            if brand |> String.notEmpty then
                                tp.Brand |> String.equalsCapInsens brand
                            else
                                ids |> Array.exists ((=) tp.HPK)
                        )
                }
            )
            |> Array.filter (_.TradeProducts >> List.isEmpty >> not)
            |> Array.map (fun p ->
                let substs = p.TradeProducts |> List.collect _.Substances
                // Match by substance name only: the generic-product substances and
                // the trade-product substances are separate ZIndex records, so a
                // concentration/unit/BigRational rounding difference would make
                // structural (=) drop a substance that is in fact the same one.
                { p with
                    Substances =
                        p.Substances
                        |> Array.filter (fun s ->
                            substs |> List.exists (fun ts -> ts.Name |> String.equalsCapInsens s.Name)
                        )
                }
            )


    /// <summary>
    /// Filter the Product array to get all the products
    /// </summary>
    /// <param name="mapping">The route mapping</param>
    /// <param name="route">The route</param>
    /// <param name="name">The Filter</param>
    /// <param name="form"></param>
    /// <param name="brand"></param>
    /// <param name="gpks"></param>
    /// <param name="hpks"></param>
    /// <param name="prods">The array of Products</param>
    let filter mapping route name form brand gpks hpks (prods: ProductComponent[]) =
        let eqsRoute = Mapping.eqsRoute mapping

        let repl s =
            s
            |> String.replace "/" ""
            |> String.replace "+" ""
            |> String.replace "(" ""
            |> String.replace ")" ""
            |> String.trim

        let eqsIfNotEmpty s1 s2 =
            if s1 |> String.isNullOrWhiteSpace then
                true
            else
                let s1 = s1 |> repl
                let s2 = s2 |> repl
                s1 |> String.equalsCapInsens s2

        let filterGPK gpk =
            if gpks |> Array.length = 0 then
                true
            else
                gpks |> Array.exists ((=) gpk)

        prods
        |> Array.filter (fun p ->
            p.Generic |> eqsIfNotEmpty name
            && p.Routes |> Array.exists (eqsRoute (Some route))
            && p.Form |> eqsIfNotEmpty form
            && filterGPK p.GPK
        )
        |> filterOutTradeProducts brand hpks


    /// Get all Generics from the given Product array.
    let generics (products: ProductComponent array) =
        products |> Array.map _.Generic |> Array.distinct


    /// Get all Synonyms from the given Product array.
    let synonyms (products: ProductComponent array) =
        products
        |> Array.collect _.Synonyms
        |> Array.append (generics products)
        |> Array.distinct


    /// Get all pharmaceutical forms from the given Product array.
    let forms (products: ProductComponent array) =
        products |> Array.map _.Form |> Array.distinct
