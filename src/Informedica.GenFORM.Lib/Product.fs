namespace Informedica.GenForm.Lib


module Product =


    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib
    open Informedica.Logging.Lib
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges

    open Utils

    module GenPresProduct = Informedica.ZIndex.Lib.GenPresProduct
    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData
    module ATCGroup = Informedica.ZIndex.Lib.ATCGroup


    let parseFormularyProducts (data: string[][]) : Result<FormularyProduct[], Message list> =
        try
            let getColumn = data |> Array.head |> Csv.getStringColumn

            let products =
                data
                |> Array.tail
                |> Array.map (fun r ->
                    let get = getColumn r

                    let getOpt col =
                        let s = get col
                        if s |> String.isNullOrWhiteSpace then None else s |> Some

                    // A flag column holds x or TRUE for true, and nothing or FALSE for false, in any case.
                    let getFlag col =
                        let s = get col

                        match s |> String.toLower with
                        | "x"
                        | "true" -> true
                        | ""
                        | "false" -> false
                        | _ ->
                            let gpk = get "GPKODE"

                            $"Formulary column %s{col} holds '%s{s}' for GPKODE %s{gpk}: expected x, TRUE or FALSE"
                            |> System.FormatException
                            |> raise

                    // Extract departments from the columns marked with 'x'
                    let departments =
                        [ "UMCU"; "ICC"; "NEO"; "ICK"; "HCK" ]
                        |> List.filter (fun dept -> (get dept) = "x")

                    // Maps the Type column to a product type; an unknown value gives NoProduct.
                    let getProdType s =
                        match s with
                        | "medication" -> ProductType.MedicationProduct
                        | "enteral" -> ProductType.EnteralProduct
                        | "parenteral" -> ProductType.ParenteralProduct
                        | _ -> ProductType.NoProduct

                    let prodType = get "Type" |> getProdType

                    let routes =
                        get "Routes"
                        |> String.splitAt ';'
                        |> Array.map String.trim
                        |> Array.filter String.notEmpty

                    // A virtual product, a parenteral or an enteral row, has its form and routes only from the sheet.
                    let failOnEmptyVirtualColumn col isEmpty =
                        if prodType |> ProductType.isVirtual && isEmpty then
                            $"""Formulary column %s{col} is empty for the virtual product %s{get "Generic"}"""
                            |> System.FormatException
                            |> raise

                    failOnEmptyVirtualColumn "Form" (get "Form" |> String.isNullOrWhiteSpace)
                    // a cell of only separators gives no route either
                    failOnEmptyVirtualColumn "Routes" (routes |> Array.isEmpty)

                    // TODO: the sheet has the columns UseForm and UseBrand, which no code reads. Confirm
                    // with the sheet owner and remove the columns, or read them.
                    {
                        GPKODE = get "GPKODE"
                        ProductType = prodType
                        Departments = departments
                        Generic = get "Generic"
                        TallMan = get "TallMan"
                        Divisible = get "Divisible" |> Int32.tryParse
                        UseGenName = get "UseGenName" = "x"
                        Mmol = get "Mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                        Form = get "Form" |> Option.ofObj
                        Routes = routes
                        Brand = get "Brand" |> Option.ofObj
                        GenName = get "GenName" |> Option.ofObj
                        GStandName = get "GStand" |> Option.ofObj
                        AltName = getOpt "AltName"
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
                        CanBeUsedAsSolvent = getFlag "IsSolvent"
                        CanBeUsedAsDiluent = getFlag "IsDiluent"
                        CanBeUsedAsAdditive = getFlag "IsAdditive"
                    }
                )

            Ok products
        with exn ->
            Result.createError "getFormularyProducts" exn


    let getFormularyProducts dataUrlId =
        Web.GoogleSheets.getRowsFromSheetSync dataUrlId "Formulary"
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


        /// Returns the sheet code of a venous access: PVL, CVL, TUBE, or an empty string for any access.
        let toString =
            function
            | PVL -> "PVL"
            | CVL -> "CVL"
            // a rule never carries a tube; the form is there so the string round-trips
            | EnteralTube -> "TUBE"
            | AnyAccess -> ""


        /// Parses PVL, CVL or TUBE in any case into a venous access; any other string gives AnyAccess.
        let fromString s =
            match s with
            | _ when s |> String.equalsCapInsens "PVL" -> PVL
            | _ when s |> String.equalsCapInsens "CVL" -> CVL
            | _ when s |> String.equalsCapInsens "TUBE" -> EnteralTube
            | _ -> AnyAccess


    module FormRoute =


        let isSolution (mapping: FormRoute[]) form =
            mapping
            |> Array.tryFind (fun sr -> sr.Form |> String.equalsCapInsens form)
            |> Option.map _.IsSolution
            |> Option.defaultValue false


    /// Whether a rule for a route, a location and a department applies to a setting. The route must match
    /// when the setting gives one, the location as PatientCategory.locationMatches decides, and the department
    /// as PatientCategory.departmentMatches decides.
    let matchesSetting routeMapping loc dep rte route location department =
        route |> Mapping.eqsRoute routeMapping rte
        && PatientCategory.locationMatches location loc
        && PatientCategory.departmentMatches department dep


    /// Returns the substances of the parenteral product whose generic name equals the name, ignoring case,
    /// and none when there is no such product.
    let substancesOf (parenteral: ProductComponent[]) name =
        parenteral
        |> Array.tryFind (_.Generic >> String.equalsCapInsens name)
        |> Option.map _.Substances
        |> Option.defaultValue [||]


    /// Returns the substances of a volume of a liquid mixed with a volume of an added liquid, in a total
    /// volume. Each concentration and molar concentration of the liquid is multiplied by the volume of the
    /// liquid, and of the added liquid by the added volume, and divided by the total volume. A substance of
    /// the added liquid is added to the substance of the liquid with the same name, or else added as a new
    /// substance; a concentration in another unit group, such as mg against mmol, is not added.
    let mixSubstances total liquidVol (substs: SubstanceItem[]) addedVol (added: SubstanceItem[]) =
        let scale qty (s: SubstanceItem) =
            { s with
                Concentration = s.Concentration |> Option.map (fun q -> qty * q / total)
                MolarConcentration = s.MolarConcentration |> Option.map (fun q -> qty * q / total)
            }

        // a value in another unit group cannot be added, so the value of the liquid is used
        let add a b =
            match a, b with
            | Some a, Some b when ValueUnit.eqsGroup a b -> Some(a + b)
            | _ -> a |> Option.orElse b

        let addSubstance (substs: SubstanceItem[]) (s: SubstanceItem) =
            match substs |> Array.tryFindIndex (_.Name >> String.equalsCapInsens s.Name) with
            | Some i ->
                let x = substs[i]

                substs
                |> Array.updateAt
                    i
                    { x with
                        Concentration = add x.Concentration s.Concentration
                        MolarConcentration = add x.MolarConcentration s.MolarConcentration
                    }
            | None -> Array.append substs [| s |]

        added
        |> Array.map (scale addedVol)
        |> Array.fold addSubstance (substs |> Array.map (scale liquidVol))


    module Reconstitution =


        /// Parses the Reconstitution sheet; the first row holds the column names. A row without a solvent or
        /// a solvent volume above 0 mL is left out. Returns an error when a column is missing.
        let parseReconstitution data =
            try
                let getColumn = data |> Array.head |> Csv.getStringColumn

                data
                |> Array.tail
                |> Array.choose (fun r ->
                    let get = getColumn r >> String.trim
                    let getOpt = get >> fun s -> if s |> String.isNullOrWhiteSpace then None else Some s
                    let toBrOpt = BigRational.toBrs >> Array.tryHead
                    let toMl = ValueUnit.singleWithUnit Units.Volume.milliLiter

                    // a solvent volume of 0 mL or less makes no reconstitution
                    let solventVol = get "SolventVol" |> toBrOpt |> Option.filter (fun v -> v > 0N)

                    match getOpt "Solvent", solventVol with
                    | Some solvent, Some vol ->
                        Some
                            {
                                GPK = get "GPK"
                                Route = get "Route"
                                Location = getOpt "Loc"
                                Department = getOpt "Dep"
                                MinAge = get "MinAge" |> toBrOpt
                                MaxAge = get "MaxAge" |> toBrOpt
                                MinWeight = get "MinWeight" |> toBrOpt
                                MaxWeight = get "MaxWeight" |> toBrOpt
                                SolventVolume = vol |> toMl
                                ExpansionVolume = get "ExpansionVol" |> toBrOpt |> Option.map toMl
                                Solvent = solvent
                                ShelfLife =
                                    get "ShelfLife"
                                    |> toBrOpt
                                    |> Option.map (ValueUnit.singleWithUnit Units.Time.minute)
                                Notes = getOpt "Notes"
                            }
                    | _ -> None
                )
                |> Ok
            with exn ->
                Result.createError "Reconstitution.parseReconstitution" exn


        let get dataUrlId : Result<_, _> =
            Web.getDataFromSheet dataUrlId "Reconstitution" |> parseReconstitution


        /// Whether a reconstitution applies to a location, a department and a route, as matchesSetting
        /// decides.
        let matches routeMapping loc dep rte (r: Reconstitution) =
            matchesSetting routeMapping loc dep rte r.Route r.Location r.Department


        /// Returns the age range in days and the weight range in grams of a reconstitution as a patient
        /// category.
        let patientCategory (r: Reconstitution) =
            PatientCategory.ofAgeAndWeight r.MinAge r.MaxAge r.MinWeight r.MaxWeight


        /// Whether the age and the weight of a patient are in the ranges of a reconstitution.
        let matchesPatient dat r = r |> patientCategory |> PatientData.matchesAgeAndWeight dat


        let filter routeMapping (filter: DoseFilter) (rs: Reconstitution[]) =
            rs
            |> Array.filter (fun r ->
                r
                |> matches
                    routeMapping
                    filter.PatientData.Location
                    (filter.PatientData |> PatientData.department)
                    filter.Route
                && r |> matchesPatient filter.PatientData
            )


    module ProductReconstitution =


        /// Creates the product reconstitution of a reconstitution rule, with the substances of its solvent.
        let create parenteral (r: Reconstitution) =
            {
                Rule = r
                SolventSubstances = r.Solvent |> substancesOf parenteral
            }


    module Predilution =


        /// Parses predilution rules from a table of text whose first row holds the column names. A rule
        /// without a diluent, or without a product volume or a diluent volume above 0 mL, is left out. Returns
        /// no rules for an empty table, and an error when a column is missing from a table that has rules.
        let parsePredilution (data: string[][]) =
            try
                match data |> Array.tryHead with
                | None -> Ok [||]
                | Some header ->
                    let getColumn = header |> Csv.getStringColumn

                    data
                    |> Array.tail
                    |> Array.choose (fun r ->
                        let get = getColumn r >> String.trim
                        let getOpt = get >> fun s -> if s |> String.isNullOrWhiteSpace then None else Some s
                        let toBrOpt = BigRational.toBrs >> Array.tryHead

                        // a volume of 0 mL or less makes no predilution
                        let volume col = get col |> toBrOpt |> Option.filter (fun v -> v > 0N)
                        let toMl = ValueUnit.singleWithUnit Units.Volume.milliLiter

                        match getOpt "Diluent", volume "MedVol", volume "DiluentVol" with
                        | Some diluent, Some medVol, Some diluentVol ->
                            Some
                                {
                                    GPK = get "GPK"
                                    Route = get "Route"
                                    Indication = getOpt "Indication"
                                    Location = getOpt "Loc"
                                    Department = getOpt "Dep"
                                    MinAge = get "MinAge" |> toBrOpt
                                    MaxAge = get "MaxAge" |> toBrOpt
                                    MinWeight = get "MinWeight" |> toBrOpt
                                    MaxWeight = get "MaxWeight" |> toBrOpt
                                    MedVolume = medVol |> toMl
                                    DiluentVolume = diluentVol |> toMl
                                    Diluent = diluent
                                }
                        | _ -> None
                    )
                    |> Ok
            with exn ->
                Result.createError "Predilution.parsePredilution" exn


        let get dataUrlId = Web.getDataFromSheet dataUrlId "Predilution" |> parsePredilution


        /// Whether a predilution applies to a location, a department, a route and an indication: the setting
        /// as matchesSetting decides, and the indication, ignoring case, when the predilution names one.
        let matches routeMapping loc dep rte indication (r: Predilution) =
            matchesSetting routeMapping loc dep rte r.Route r.Location r.Department
            && r.Indication |> Option.forall (String.equalsCapInsens indication)


        /// Returns the age range in days and the weight range in grams of a predilution as a patient
        /// category.
        let patientCategory (r: Predilution) =
            PatientCategory.ofAgeAndWeight r.MinAge r.MaxAge r.MinWeight r.MaxWeight


        /// Whether the age and the weight of a patient are in the ranges of a predilution.
        let matchesPatient dat r = r |> patientCategory |> PatientData.matchesAgeAndWeight dat


    module ProductPredilution =


        /// Creates the product predilution of a predilution rule, with the substances of its diluent.
        let create parenteral (r: Predilution) =
            {
                Rule = r
                DiluentSubstances = r.Diluent |> substancesOf parenteral
            }


    let createSubstance logger name conc unit formUnit unitMapping =
        match conc with
        | Some br when br > 0N ->
            let conc =
                unit
                |> Mapping.mapUnit unitMapping
                |> function
                    | None ->
                        ErrorMsg($"cannot map unit: {unit}", None) |> Logging.logError logger
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


    /// Returns the routes of a virtual product as ZIndex names, through the route mapping. Raises a
    /// FormatException for a route that the route mapping does not know.
    let virtualRoutes routeMapping (fp: FormularyProduct) =
        fp.Routes
        |> Array.map (fun r ->
            match r |> Mapping.mapRoute routeMapping with
            | Some route -> route
            | None ->
                $"Formulary route %s{r} of the virtual product %s{fp.Generic} is not in the Routes sheet"
                |> System.FormatException
                |> raise
        )


    /// Creates a virtual product from a parenteral or enteral row of the Formulary sheet, with the
    /// nutrient columns as its substances. The form is lowercased like the form of a G-Standaard product.
    /// Raises a FormatException for a substance name that is not a name and a unit.
    let createVirtualProduct logger name (form: string) routes formUnit unitMapping substs =
        {
            GPK = name
            ATC = ""
            MainGroup = ""
            SubGroup = ""
            Generic = name
            TallMan = ""
            Synonyms = [||]
            ProductLabels = [ name ]
            Label = name
            Form = form |> String.toLower
            Routes = routes
            FormQuantities = formUnit |> ValueUnit.singleWithValue 1N
            FormUnit = formUnit
            RequiresReconstitution = false
            Reconstitution = [||]
            Predilution = None
            Divisible = Some 10N
            Substances =
                substs
                |> List.choose (fun (s, q) ->
                    let n, u =
                        match s |> String.split " " with
                        | [ n; u ] -> n |> String.trim, u |> String.trim
                        | _ -> raise (System.FormatException $"cannot parse substance {s}")

                    createSubstance logger n q u formUnit unitMapping
                )
                |> List.filter (fun s ->
                    s.Name |> String.notEmpty
                    && (s.Concentration |> Option.isSome || s.MolarConcentration |> Option.isSome)
                )
                |> List.toArray
            TradeProducts = []
        }


    module Enteral =


        let get logger unitMapping routeMapping (prods: FormularyProduct[]) =
            prods
            |> Array.filter _.ProductType.IsEnteralProduct
            |> Array.choose (fun fp ->
                fp.Unit
                |> Option.bind UnitsParse.fromString
                |> Option.map (fun fu ->
                    fp
                    |> getAdditionalSubstances
                    |> createVirtualProduct
                        logger
                        fp.Generic
                        (fp.Form |> Option.defaultValue "")
                        (fp |> virtualRoutes routeMapping)
                        fu
                        unitMapping
                )
            )


    module Parenteral =


        let get logger unitMapping routeMapping (prods: FormularyProduct[]) =
            prods
            |> Array.filter _.ProductType.IsParenteralProduct
            |> Array.map (fun fp ->
                fp
                |> getAdditionalSubstances
                |> createVirtualProduct
                    logger
                    fp.Generic
                    (fp.Form |> Option.defaultValue "")
                    (fp |> virtualRoutes routeMapping)
                    Units.Volume.milliLiter
                    unitMapping
            )


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
            Predilution = None
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
        logger
        unitMapping
        routeMapping
        formRoutes
        (reconstitution: ProductReconstitution[])
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

        let reqReconst = Mapping.requiresReconstitution routeMapping formRoutes (gp.Route, formUnit, gp.Form)

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
            Reconstitution = reconstitution |> Array.filter (fun r -> r.Rule.GPK = $"{gp.Id}")
            Predilution = None
            Divisible =
                match fpOpt |> Option.map _.Divisible |> Option.defaultValue None with
                | Some d -> d |> BigRational.fromInt |> Some
                | None ->
                    let rs = Mapping.filterFormRoutes routeMapping formRoutes "" (gp.Form.ToLower()) NoUnit

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

                            createSubstance logger n q u formUnit unitMapping
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
                        HPK = tp.Id |> string
                        Brand = tp.Brand
                        Substances = tp.Substances |> Array.map mapSubst |> Array.toList

                    }
                )
                |> Array.toList
        }


    /// The G-Standaard name of a generic product: the GStand column of its Formulary row, else the Generic column,
    /// lowercased; the ZIndex name when the product has no Formulary row.
    let gstandName (fp: FormularyProduct option) (gp: Informedica.ZIndex.Lib.Types.GenericProduct) =
        fp
        |> Option.map (fun fp -> fp.GStandName |> Option.defaultValue fp.Generic |> String.toLower)
        |> Option.defaultValue gp.Name


    /// The names of the virtual products: the Generic of every parenteral and enteral row, lowercased.
    let virtualProductNames (formularyProducts: FormularyProduct[]) =
        formularyProducts
        |> Array.filter (_.ProductType >> ProductType.isVirtual)
        |> Array.map (_.Generic >> String.toLower)
        |> Set.ofArray


    /// Returns the name of a generic product: the AltName of its Formulary row as written, else its
    /// G-Standaard name. An AltName that is also the name of a virtual product is not used, so the
    /// parenteral fluids keep their virtual products until #1444 replaces them.
    let productName virtualNames fp gp =
        fp
        |> Option.bind _.AltName
        |> Option.filter (fun n -> virtualNames |> Set.contains (n |> String.toLower) |> not)
        |> Option.defaultValue (gstandName fp gp)


    /// <summary>
    /// Builds the product components from ZIndex GenPresProducts that are already loaded, and adds the
    /// parenteral and enteral virtual products. Performs no IO.
    /// </summary>
    let fromGenPresProducts
        logger
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
        let reconstitution = reconstitution |> Array.map (ProductReconstitution.create parenteral)

        // A lookup from GPK to Formulary row, built once, so each generic product is matched in constant time.
        let formByGpk =
            formularyProducts
            |> Array.choose (fun fp -> fp.GPKODE |> Int32.tryParse |> Option.map (fun gpk -> gpk, fp))
            |> Map.ofArray

        let virtualNames = formularyProducts |> virtualProductNames

        // Every generic product with a valid form and a substance quantity above 0, also one without a
        // Formulary row, paired with its Formulary row by GPK or with None.
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

        // Builds one product component per pair of Formulary row and generic product.
        let buildProduct (fp: FormularyProduct option, gp: Informedica.ZIndex.Lib.Types.GenericProduct) =
            // The G-Standaard name selects the substance that the Mmol column applies to. The rules
            // select the product by its product name, the AltName when there is one. A brand or form
            // suffix in the Formulary Generic stays out of both names and goes into the label.
            let name = gstandName fp gp

            // The label is the Formulary Generic when it differs from the G-Standaard name, else the
            // ZIndex label.
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
            |> map logger unitMapping routeMapping formRoutes reconstitution name synonyms formQuantities fp
            |> fun prod ->
                { prod with
                    Generic = productName virtualNames fp gp
                    Label = displayLabel
                }

        // Build the products in parallel, because buildProduct takes most of the time. Build the first
        // chunk on one thread, so the static initialization of the unit modules runs once: a first use
        // of those modules from several threads at once can deadlock.
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
    /// Reads the ZIndex GenPresProducts and builds the product components with fromGenPresProducts,
    /// timing the call.
    /// </summary>
    let get
        logger
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
                logger
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
    /// Keeps only the ZIndex generic products that the dose rule data can select, so fewer product
    /// components are built. A generic product is kept when its GPK is in the GPKs of a row, when one
    /// of its trade products is in the HPKs of a row, or, for a row with neither, when its product name
    /// is the component name of the row and one of its routes is the route of the row. Returns the
    /// GenPresProducts with only the kept generic products, and leaves out a GenPresProduct with none.
    /// Product.filter then selects the products per rule from this result, so the dose rules are the
    /// same as without this filter.
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
            |> Array.choose (fun fp -> fp.GPKODE |> Int32.tryParse |> Option.map (fun g -> g, fp))
            |> Map.ofArray

        let virtualNames = formularyProducts |> virtualProductNames

        // the same normalization that Product.filter uses to compare names
        let canon (s: string) =
            s
            |> String.replace "/" ""
            |> String.replace "+" ""
            |> String.replace "(" ""
            |> String.replace ")" ""
            |> String.trim
            |> String.toLower

        // indices built once from the data: a row with GPKs or HPKs is indexed by those ids, a row with
        // neither by its component name and route
        let gpkSet = System.Collections.Generic.HashSet<string>()
        let hpkSet = System.Collections.Generic.HashSet<string>()
        // for a row without GPKs or HPKs: the mapped route to the set of normalized component names
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
            || (let name = productName virtualNames (formByGpk |> Map.tryFind gp.Id) gp |> canon

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


    /// <summary>Reconstitutes a product for a location, a department and a route.</summary>
    /// <param name="mapping">The route mapping.</param>
    /// <param name="loc">The location.</param>
    /// <param name="dep">The department.</param>
    /// <param name="fits">Whether a reconstitution applies to the patient.</param>
    /// <param name="rte">The route; an empty route is not checked.</param>
    /// <param name="prod">The product.</param>
    /// <returns>
    /// The product unchanged when it requires no reconstitution, else one product per matching
    /// reconstitution rule: 1 mL of the product mixed with the solvent volume of solvent in the solvent
    /// volume plus the expansion volume, as mixSubstances mixes the substances. A reconstituted product holds
    /// only the reconstitution it was made with. Also returns a warning when no reconstitution rule matches.
    /// </returns>
    let reconstitute mapping loc dep fits rte (prod: ProductComponent) =
        let warnings = ResizeArray<string>()
        let rte' = rte |> Mapping.routeOption
        let one = Units.Volume.milliLiter |> ValueUnit.singleWithValue 1N

        let prods =
            [|
                // a product that requires no reconstitution is returned unchanged
                if prod.RequiresReconstitution |> not then
                    [| prod |]
                else
                    // calculate the reconstituted products
                    prod.Reconstitution
                    |> Array.filter (fun r -> r.Rule |> Reconstitution.matches mapping loc dep rte' && r.Rule |> fits)
                    |> fun xs ->
                        if xs |> Array.isEmpty then
                            warnings.Add
                                $"no reconstitution rules found for {prod.Generic} ({prod.Form}) with route {rte} and department {dep}"

                        xs
                    |> Array.map (fun r ->
                        let vol =
                            r.Rule.ExpansionVolume
                            |> Option.map (fun v -> v + r.Rule.SolventVolume)
                            |> Option.defaultValue r.Rule.SolventVolume

                        { prod with
                            FormUnit = Units.Volume.milliLiter
                            FormQuantities = vol
                            Substances =
                                r.SolventSubstances
                                |> mixSubstances vol one prod.Substances r.Rule.SolventVolume
                            Reconstitution = [| r |]
                        }
                    )
            |]
            |> Array.collect id

        prods, warnings |> Seq.distinct


    /// Returns the product followed by one pre-diluted product per matching predilution of its GPK: the
    /// product volume of the product with the diluent volume of diluent, in the prepared volume of the two,
    /// as mixSubstances mixes the substances. The product is a liquid: a liquid product, or a product after
    /// the reconstitution. The label of a pre-diluted product names the predilution, for example
    /// "..., 3 mL verdund met 7 mL gluc 5%".
    let predilute mapping loc dep fits rte indication (preds: ProductPredilution[]) (prod: ProductComponent) =
        let rte' = rte |> Mapping.routeOption
        let toString = ValueUnit.toStringDecimalDutchShortWithPrec -1

        let prediluted =
            preds
            |> Array.filter (fun p ->
                p.Rule.GPK = prod.GPK
                && p.Rule |> Predilution.matches mapping loc dep rte' indication
                && p.Rule |> fits
            )
            |> Array.map (fun p ->
                let vol = p.Rule.MedVolume + p.Rule.DiluentVolume
                let medVol = p.Rule.MedVolume |> toString
                let diluentVol = p.Rule.DiluentVolume |> toString

                { prod with
                    Label = $"%s{prod.Label}, %s{medVol} verdund met %s{diluentVol} %s{p.Rule.Diluent}"
                    FormQuantities = vol
                    Substances =
                        p.DiluentSubstances
                        |> mixSubstances vol p.Rule.MedVolume prod.Substances p.Rule.DiluentVolume
                    Predilution = Some p
                }
            )

        Array.append [| prod |] prediluted


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
                // Match the substances by name only. The substances of the generic product and of the trade
                // product are separate ZIndex records, so a small difference in concentration, unit or
                // rounding would make structural equality drop the same substance.
                { p with
                    Substances =
                        p.Substances
                        |> Array.filter (fun s ->
                            substs |> List.exists (fun ts -> ts.Name |> String.equalsCapInsens s.Name)
                        )
                }
            )


    /// Returns the products with one of the GPKs, and of those the trade products of the brand, or with one of the
    /// HPKs when no brand is given. Empty GPKs keep every product; an empty brand and empty HPKs keep every trade
    /// product. When a brand or HPKs are given, a product without a matching trade product is left out.
    let narrow brand gpks hpks (prods: ProductComponent[]) =
        prods
        |> Array.filter (fun p -> gpks |> Array.isEmpty || gpks |> Array.contains p.GPK)
        |> filterOutTradeProducts brand hpks


    /// <summary>
    /// Returns the products that match the route, and the name, form and GPKs when given, with their trade
    /// products narrowed by brand or HPKs. When a brand or HPKs are given, a product without a matching
    /// trade product is left out.
    /// </summary>
    /// <param name="mapping">The route mapping.</param>
    /// <param name="route">The route.</param>
    /// <param name="name">The generic name; empty matches any name. Ignores case and / + ( ).</param>
    /// <param name="form">The pharmaceutical form; empty matches any form, compared like the name.</param>
    /// <param name="brand">The brand of the trade products to keep; empty keeps every brand.</param>
    /// <param name="gpks">The GPKs to keep; empty keeps every GPK.</param>
    /// <param name="hpks">The HPKs of the trade products to keep when no brand is given; empty keeps every HPK.</param>
    /// <param name="prods">The products.</param>
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

        prods
        |> Array.filter (fun p ->
            p.Generic |> eqsIfNotEmpty name
            && p.Routes |> Array.exists (eqsRoute (Some route))
            && p.Form |> eqsIfNotEmpty form
        )
        |> narrow brand gpks hpks


    /// Returns the distinct generics of the products.
    let generics (products: ProductComponent array) = products |> Array.map _.Generic |> Array.distinct


    /// Returns the distinct synonyms and generics of the products.
    let synonyms (products: ProductComponent array) =
        products
        |> Array.collect _.Synonyms
        |> Array.append (generics products)
        |> Array.distinct


    /// Returns the distinct pharmaceutical forms of the products.
    let forms (products: ProductComponent array) = products |> Array.map _.Form |> Array.distinct
