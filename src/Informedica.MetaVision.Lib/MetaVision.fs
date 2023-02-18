namespace Informedica.MetaVision.Lib

#nowarn "0058"




module MetaVision =


    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.ZIndex.Lib


    module Frequency =


        let toString = function
            | Continuous -> "Continue"
            | DosePerDay -> "Doses/dag instellen"
            | DosePerWeek  -> "Doses/week instellen"
            | Once -> "eenmalig"
            | SelectedTimes -> "Geselecteerde tijden"
            | SetInterval -> "Setel interval in"


        let map s =
            match s with
            | _ when s = "eOnce" -> "eenmalig"
            | _ when s = "eContinuous" -> "Continue"
            | _ -> s


    let entFeeding =
        mapEntFeeding
        |> Array.skip 1
        |> Array.map (fun xs -> Data.enteral xs[0] xs[1])


    let parentMeds =
        mapParentMeds
        |> Array.skip 1
        |> Array.map (fun xs -> Data.parenteral xs[0])


    let getDrugFamilies path =
        ATCGroup.get ()
        |> Array.map (fun g ->
            g.AnatomicalGroup |> capitalize,
            g.TherapeuticSubGroup |> capitalize
        )
        |> Array.distinct
        |> Array.sort
        |> Array.map (fun (m, s) -> $"{m}\t{s}")


    let routeShapeUnits () =
        GenPresProduct.get true
        //|> Array.filter (fun gpp -> gpp.Shape |> String.toLower |> String.contains "concentraat" )
        |> Array.collect (fun gpp -> gpp.GenericProducts)
        |> Array.collect (fun gp ->
            gp.Route
            |> Array.map (fun r ->
                r |> String.toLower |> String.trim
                , gp.Shape |> String.toLower |> String.trim
                , gp.Substances[0].ShapeUnit |> String.toLower |> String.trim
            )
        )
        |> Array.distinct
        |> Array.map (fun (r, s, u) -> $"{r}\t{s}\t{u}")


    let createRoutes file newFile name =
        let mapRts = (Array.mapStringHeadings Data.routeHeadings) >> (String.concat "\t")
        // Get routes and external codes
        // Intra seperated by -
        let rts =
            Names.getItems Names.Route Names.Fifty
            |> Array.filter (fun (_, r) -> r.Contains(",") |> not)
            |> Array.sortBy fst
            |> Array.distinct

        rts
        |> Array.filter (fun (_, r) -> r |> mapRoute |> String.isNullOrWhiteSpace |> not)
        |> Array.map (fun (id, r) ->
            [|
                "ExternalCode", $"%i{id}"
                "RouteName", r |> mapRoute
                "OrderingType",
                    mappingRouteShape
                    |> Array.filter (fun xs -> r |> String.toLower = xs[0] )
                    |> Array.fold (fun acc xs ->
                        match acc with
                        | NonInfuse ->
                            if xs[2] |> String.contains Constants.NoTime then NonInfuse
                            else Both
                        | Both -> Both
                    ) NonInfuse
                    |> string
            |]
            |> mapRts
        )
        |> Array.append [| Data.routeHeadings |> String.concat "\t" |]
        |> createDataImport file newFile name

        rts
        |> Array.map snd
        |> Array.map mapRoute
        |> Array.filter (String.isNullOrWhiteSpace >> not)


    let createDoseForms file newFile name routes =
        let mapForms = (Array.mapStringHeadings Data.doseFormHeadings) >> (String.concat "\t")

        // Get doseforms
        Names.getItems Names.Shape Names.Fifty
        |> Array.distinct
        |> Array.map (fun (id, s) ->
            let s = s |> String.toLower
            let rts =
                GenPresProduct.get true
                |> Array.filter (fun gpp -> gpp.Shape |> String.equalsCapInsens s)
                |> Array.collect (fun gpp -> gpp.Routes)
                |> Array.collect (String.splitAt ',')
                |> Array.filter (mapRoute >> String.isNullOrWhiteSpace >> not)
                |> Array.distinct

            {
                ExternalCode = id
                DoseFormName = s |> String.toLower
                Routes = rts
                OrderingType =
                    mappingRouteShape
                    |> Array.filter (fun xs ->
                        rts
                        |> Array.exists (fun rt ->
                            rt |> String.equalsCapInsens xs[0]
                        ) &&
                        s |> String.toLower = xs[1]

                    )
                    |> Array.fold (fun acc xs ->
                        match acc with
                        | NonInfuse ->
                            if xs[4] = Constants.TRUE then Both
                            else acc
                        | Both -> Both
                    ) NonInfuse
                IsDrugInSolution =
                    s |> shapeIsSolution "" "" ||
                    s |> shapeInDiluent "" ""
                Category = Constants.``G-Standaard``
                IsDispensableAmountAllowed = false
            }
        )
        |> Array.filter (fun r -> r.Routes |> Array.isEmpty |> not)
        |> Array.sortBy (fun r -> r.ExternalCode)
        |> Array.map (fun r ->
            let rs =
                r.Routes
                |> Array.map mapRoute
                |> Array.filter (String.isNullOrWhiteSpace >> not)
                |> Array.distinct
                |> String.concat ";"
            [|
                "ExternalCode", $"%i{r.ExternalCode}"
                "DoseFormName", r.DoseFormName
                "Routes", rs
                "OrderingType",$"{r.OrderingType}"
                "IsDrugInSolution", $"{r.IsDrugInSolution |> mapBool}"
                "Category", r.Category
                "IsDispensableAmountAllowed", $"{r.IsDispensableAmountAllowed |> mapBool}"

            |]
            |> mapForms
        )
        |> Array.append (
            [|
                Constants.``parenterale vloeistof``
                Constants.voeding
                Constants.``poeder voor voeding``
            |]
            |> Array.map (fun s ->
                [|
                    "DoseFormName", s
                    "Routes", routes |> String.concat ";"
                    "DefaultUnit", if s = Constants.``poeder voor voeding`` then Constants.g else Constants.mL
                    "OrderingType", $"{Both}"
                    "IsDrugInSolution", false |> mapBool
                    "Category", "Overige"
                    "IsDispensableAmountAllowed", false |> mapBool
                |]
                |> mapForms
            )
        )
        |> Array.append [| Data.doseFormHeadings |> String.concat "\t" |]
        |> createDataImport file newFile name


    let createIngredients file newFile name (gp : GenericProduct[]) =
        let mapIngrs = (Array.mapStringHeadings Data.ingredientHeadings) >> (String.concat "\t")

        let substs =
            gp
            |> Array.collect (fun gp -> gp.Substances)
            |> Array.filter (fun s ->
                s.SubstanceName |> String.equalsCapInsens Constants.water |> not &&
                s.GenericName |> String.equalsCapInsens Constants.water |> not
            )

        // Ingredients
        substs
        |> Array.map (fun s ->
            {
                ExternalCode =  $"%i{s.SubstanceId}"
                IngredientName = s.SubstanceName |> String.trim |> String.toLower
                Unit = s.SubstanceUnit |> mapUnit
            }
        )
        |> Array.distinctBy (fun r -> r.ExternalCode)
        |> Array.filter (fun r ->
            if r.Unit |> String.isNullOrWhiteSpace then
                printfn $"{r.IngredientName} has no unit"

            r.Unit |> String.isNullOrWhiteSpace |> not
        )
        |> Array.sortBy (fun r -> r.IngredientName)
        |> fun ingrs ->
            substs
            |> Array.map (fun s ->
                let su = s.ShapeUnit |> mapUnit
                {
                    ExternalCode = ""
                    IngredientName = su
                    Unit = su
                }
            )
            |> Array.distinct
            |> Array.append ingrs
        |> fun ingrs ->
                [|1..13|]
                |> Array.map (fun indx ->
                    let n, u =
                        match mapParentMeds[0][indx] |> String.splitAt ' ' with
                        | [|n;u|] -> n, u
                        | _ -> "", ""
                    {
                        ExternalCode = ""
                        IngredientName = n
                        Unit = u
                    }
                )
                |> Array.append ingrs
        |> Array.map (fun r ->
            [|
                "ExternalCode", r.ExternalCode
                "IngredientName", r.IngredientName
                "Unit", r.Unit

            |]
            |> mapIngrs
        )
        |> Array.append [| Data.ingredientHeadings |> String.concat "\t" |]
        |> createDataImport file newFile name


    let orderingStyleToString = function
        | NoInfusedOver -> "Geen looptijd", "No Infuse Over Drug - Calculate Total Volume"
        | SpecifyInfuseOver -> "Looptijd specificeren", "Specify Infuse Over Drug - Set Infuse Over"
        | SetDoseAndRate -> "Dosis en snelheid instellen", "Set Dose and Rate Drug - Set Dose with Total Volume"


    let createTemplates file newFile (meds : Medication[]) =
        let mapTempl = (Array.mapStringHeadings Data.orderTemplateHeadings) >> (String.concat "\t")

        let templates =
            meds
            |> Array.collect (fun m ->
                if m.Assortment |> Array.length > 1 then
                    m.Assortment
                    |> Array.filter ((<>) UMCU)
                else m.Assortment
                |> Array.collect (fun d ->
                    m.Routes
                    |> String.splitAt ';'
                    |> Array.collect (fun r ->
                        if m.Products |> Array.isEmpty then
                            let dnv =
                                let gpk = m.ExternalCode |> String.replace Constants.``GPK-`` ""
                                getReconsitiution gpk r $"{d}"

                            [|
                                {|
                                    Department = d
                                    Medication = m
                                    Product = ""
                                    DiluentName = dnv |> Option.map snd |> Option.defaultValue ""
                                    DiluentVolume = dnv |> Option.map fst
                                    Route = r
                                |}
                            |]
                        else
                            m.Products
                            |> Array.map (fun p ->
                                {|
                                    Department = d
                                    Medication = m
                                    Product = p.ProductName
                                    DiluentName = p.DiluentName
                                    DiluentVolume = Some 1m
                                    Route = r
                                |}
                            )
                    )
                )
            )

        templates
        |> Array.map (fun t ->
            let ordStyle =
                if t.Medication.IsSolution then
                    if t.Medication.Frequencies = Constants.eContinuous then SetDoseAndRate
                    else
                        let b =
                            t.Medication.DoseForms
                            |> shapeIsInfuseOver
                        if b then SpecifyInfuseOver else NoInfusedOver
                else NoInfusedOver
            let m = t.Medication
            let p = t.Product
            let r = t.Route
            let f =
                m.Frequencies
                |> String.splitAt ';'
                |> Array.filter (fun s ->
                    if ordStyle = SpecifyInfuseOver ||
                       ordStyle = NoInfusedOver then s <> Constants.eContinuous
                    else true
                )
                |> Array.filter ((<>) Constants.``[All]``)
                |> Array.tryHead
                |> Option.defaultValue "01x per dag (00)"
                |> Frequency.map
            let n = $"A#{t.Department}|R#{r}|D#{f}"

            {
                OrderTemplateName = n
                MedicationName = m.MedicationName
                ProductName = p
                DoseForm = m.DoseForms
                Route = r
                IsPRN = Constants.FALSE
                PatternMode = $"{Standard}"
                Frequency = f
                ComponentType = Constants.MainComponent
                OrderingStyle = ordStyle |> orderingStyleToString |> fst
                LockerTemplate = ordStyle |> orderingStyleToString |> snd
                ComponentMedicationName =
                    if ordStyle = NoInfusedOver then ""
                    else
                        if p |> String.isNullOrWhiteSpace then m.MedicationName
                        else ""
                ComponentProductName =
                    if ordStyle = NoInfusedOver then ""
                    else
                        if p |> String.isNullOrWhiteSpace then ""
                        else p
                ComponentQuantityVolumeValue =
                    if m.Unit = Constants.keer ||
                       m.Unit = Constants.druppel ||
                       m.Unit = Constants.dosis then 1m
                    else
                        match m.ComplexMedications |> Array.tryHead with
                        | Some cm -> cm.Concentration
                        | None -> 0m
                ComponentQuantityVolumeUnit =
                    if m.Unit = Constants.keer ||
                       m.Unit = Constants.dosis ||
                       m.Unit = Constants.druppel then m.Unit
                    else
                        match m.ComplexMedications |> Array.tryHead with
                        | Some cm -> cm.ConcentrationUnit
                        | None -> ""
                ComponentConcentrationMassUnit =
                    if m.Unit = Constants.keer || m.Unit = Constants.dosis then m.Unit
                    else
                        if m.Unit = Constants.druppel then ""
                        else
                            match m.ComplexMedications |> Array.tryHead with
                            | Some cm -> cm.ConcentrationUnit
                            | None -> ""
                ComponentConcentrationVolumeUnit =
                    if m.Unit = Constants.druppel || m.Unit = Constants.mL then ""
                    else Constants.mL
                ComponentDrugInDiluentDiluentMedicationName = t.DiluentName
                ComponentDrugInDiluentVolumeValue = t.DiluentVolume
                ComponentDrugInDiluentVolumeUnit = if t.DiluentName |> String.isNullOrWhiteSpace then "" else Constants.mL
                TotalVolumeUnit = Constants.mL
                StartMethod =
                    match f with
                    | _ when f = Constants.eenmalig -> Constants.Nu
                    | _ when f = Constants.Continue -> Constants.Nu
                    | _ -> Constants.``Volgende geplande dosis``
                EndMethod = if f = Constants.eenmalig then "" else Constants.``Geen tijdslimiet``
                WeightType = Constants.ActualWeight
                Comment = ""
                Caption = m.Title
                AvailableInRT = Constants.TRUE
            }
        )
        |> Array.filter (fun r -> r.ComponentQuantityVolumeValue > 0m)
        |> Array.map (fun r ->
            [|
                "OrderTemplateName", r.OrderTemplateName
                "MedicationName", r.MedicationName
                "ProductName", r.ProductName
                "DoseForm", r.DoseForm
                "Route", r.Route
                "IsPRN", r.IsPRN
                "PatternMode",r.PatternMode
                "Frequency", r.Frequency
                "ComponentType", r.ComponentType
                "OrderingStyle", r.OrderingStyle
                "OrderTemplateName", r.OrderTemplateName
                "ComponentMedicationName", r.ComponentMedicationName
                "ComponentProductName", r.ComponentProductName
                "ComponentQuantityVolumeValue", $"{r.ComponentQuantityVolumeValue |> Decimal.toStringNumberNLWithoutTrailingZeros}"
                "ComponentQuantityVolumeUnit", r.ComponentQuantityVolumeUnit
                "ComponentConcentrationMassUnit", r.ComponentConcentrationMassUnit
                "ComponentConcentrationVolumeUnit", r.ComponentConcentrationVolumeUnit
                "ComponentDrugInDiluentDiluentMedicationName", r.ComponentDrugInDiluentDiluentMedicationName
                "ComponentDrugInDiluentVolumeValue",
                    r.ComponentDrugInDiluentVolumeValue
                    |> Option.map Decimal.toStringNumberNLWithoutTrailingZeros
                    |> Option.defaultValue ""
                "ComponentDrugInDiluentVolumeUnit", r.ComponentDrugInDiluentVolumeUnit
                "TotalVolumeUnit", r.TotalVolumeUnit
                "StartMethod", r.StartMethod
                "EndMethod", r.EndMethod
                "WeightType", r.WeightType
                "Comment", r.Comment
                "Caption", r.Caption
                "AvailableInRT", r.AvailableInRT
            |]
            |> mapTempl
        )
        |> createDataImport file newFile "OrderTemplates"


    let createMedications includeAssort file newFile ingrName medName complName brandName prodName meds =
        let mapMeds = (Array.mapStringHeadings Data.medicationHeadings) >> (String.concat "\t")
        let mapComp = (Array.mapStringHeadings Data.complexMedicationHeadings) >> (String.concat "\t")
        let mapBrand = (Array.mapStringHeadings Data.brandHeadings) >> (String.concat "\t")
        let mapProd = (Array.mapStringHeadings Data.productHeadings) >> (String.concat "\t")

        let gps =
            meds
            |> filterGenericProducts

        gps |> createIngredients file newFile ingrName

        let meds =
            gps
            |> Array.map (fun gp ->
                let drs =
                    RuleFinder.createFilter
                        None
                        None
                        None
                        (Some gp.Id)
                        ""
                        ""
                        ""
                    |> RuleFinder.find true

                let name =
                    gp.Name
                    |> String.trim
                    |> String.toLower
                    |> String.replace "'" ""
                    |> String.replace "  " " "

                let grps =
                    ATCGroup.get ()
                    |> Array.filter (fun g ->
                        g.ATC5
                        |> String.contains (gp.ATC |> String.subString 0 4)
                    )
                    |> Array.tryHead

                let rts =
                    gp.Route
                    |> Array.collect (String.splitAt ',')
                    |> Array.filter ((String.equalsCapInsens Constants.Parenteraal) >> not)
                    |> Array.distinct

                let su =
                    gp.Substances[0].ShapeUnit
                    |> mapUnit

                let un =
                    match gp.Shape |> shapeDoseUnit rts gp.Substances[0].ShapeUnit with
                    | Some u when u |> String.isNullOrWhiteSpace |> not -> u
                    | _ -> gp.Substances[0].SubstanceUnit
                    |> mapUnit

                let assort = gp.Id |> getFormulary

                {
                    Medication.ExternalCode = $"{Constants.``GPK-``}{gp.Id}"
                    MedicationName = name
                    Title =
                        if gp.Substances |> Array.length > 4 then name
                        else
                            gp.Substances
                            |> Array.map (fun s -> s.SubstanceName)
                            |> String.concat "/"
                            |> String.toLower

                    Unit = un
                    ATC =
                        gp
                        |> getSynonyms
                        |> Array.append [| gp.ATC |]
                        |> Array.map String.trim
                        |> Array.filter (String.isNullOrWhiteSpace >> not)
                        |> Array.distinct
                        |> Array.map (String.replace "'" "")
                        |> Array.append (assort |> Array.filter ((<>) UMCU) |> Array.map string)
                        |> String.concat ", "
                    Status = Active
                    Format =
                        if un = Constants.keer || un = Constants.druppel || un = Constants.dosis then Constants.``1,234``
                        else
                            Constants.``1,234.56``
                    IncrementValue = 0.1m
                    CodeSnippetName = $"{Constants.``GPK-``}{gp.Id} {System.Guid.NewGuid().ToString()}"
                    Frequencies =
                        let freqs = drs |> getFrequencies (gp.Shape |> shapeIsInfuseOver)
                        if freqs |> String.isNullOrWhiteSpace then Constants.``[All]``
                        else freqs
                    DoseForms = gp.Shape |> String.toLower |> String.trim
                    Routes =
                        rts
                        |> Array.map mapRoute
                        |> Array.filter (String.isNullOrWhiteSpace >> not)
                        |> String.concat ";"
                    AdditivesGroup = Constants.``[None]``
                    DiluentsGroup = // "Verdunnen"
                        if gp.Shape |> shapeIsSolution "" gp.Substances[0].ShapeUnit then Constants.diluteGroup
                        else Constants.``[None]``
                    DrugInDiluentGroup = // "Oplossen"
                        if gp.Shape |> shapeIsSolution "" un then Constants.``[None]``
                        else
                            if gp.Shape |> shapeInDiluent "" gp.Substances[0].ShapeUnit ||
                               gp.Shape |> shapeIsSolution "" gp.Substances[0].ShapeUnit then Constants.solveGroup
                            else
                                Constants.``[None]``
                    DrugFamily =
                        grps
                        |> Option.map (fun g -> drugFamilyName g.ATC1 g.AnatomicalGroup)
                        |> Option.defaultValue ""
                    DrugSubfamily =
                        grps
                        |> Option.map (fun g -> drugFamilyName g.ATC3 g.TherapeuticSubGroup)
                        |> Option.defaultValue ""
                    Assortment = assort
                    IsFormulary = assort |> Array.isEmpty |> not
                    CreateProduct =
                        un <> Constants.keer &&
                        gp.Substances[0].ShapeUnit |> isSolutionUnit

                    ComplexMedications =
                        if gp.Substances |> Array.length > 4 ||
                           un = Constants.keer then [||]
                        else
                            let cms =
                                gp.Substances
                                |> Array.map (fun s ->
                                    let q, u = s.SubstanceQuantity, s.SubstanceUnit
                                    let u = u |> mapUnit

                                    {
                                        ComplexMedictionName = name
                                        IngredientName =
                                            s.SubstanceName
                                            |> String.toLower
                                            |> String.trim
                                        Concentration = q
                                        ConcentrationUnit = u
                                        In =
                                            if (gp.Shape |> shapeIsSolution "" un || un = Constants.dosis) &&
                                               un <> u then "1" else ""
                                        InUnit =
                                            if gp.Shape |> shapeIsSolution "" un |> not &&
                                               un <> Constants.dosis then ""
                                            else
                                                 if un = Constants.druppel then Constants.mL else un
                                    }
                                )
                                |> Array.groupBy (fun cm -> cm.IngredientName)
                                |> Array.collect (fun (_, cms) ->
                                    match cms |> Array.tryHead with
                                    | None -> [||]
                                    | Some cm ->
                                        [|
                                            { cm with
                                                Concentration =
                                                    cms
                                                    |> Array.sumBy (fun cm -> cm.Concentration)
                                            }
                                        |]

                                )

                            if gp.Substances[0].ShapeUnit |> isSolutionUnit ||
                               un |> isSolutionUnit || un = su then [||]
                            else
                                [|
                                    {
                                        ComplexMedictionName = name
                                        IngredientName = su
                                        Concentration = 1m
                                        ConcentrationUnit = su
                                        In = ""
                                        InUnit = ""
                                    }
                                |]
                            |> Array.append cms

                    Brands =
                        gp.PrescriptionProducts
                        |> Array.collect (fun pp ->
                            pp.TradeProducts
                            |> Array.map (fun tp -> tp.Brand)
                        )
                        |> Array.filter (String.isNullOrWhiteSpace >> not)
                        |> Array.distinct

                    Products = [||]

                    IsSolution =
                        gp.Shape |> shapeIsSolution "" gp.Substances[0].ShapeUnit ||
                        gp.Shape |> shapeIsSolution "" un

                }
            )
            |> Array.filter (fun r -> r.Routes |> String.isNullOrWhiteSpace |> not)
            |> Array.sortBy (fun r -> r.MedicationName)
            |> Array.map (fun r ->
                { r with
                    Products =
                        if r.CreateProduct |> not ||
                           r.Unit = Constants.druppel || r.Unit = Constants.mL ||
                           r.ComplexMedications |> Array.isEmpty then [||]
                        else
                            [|
                                {
                                    Id = r.ExternalCode |> String.replace Constants.GPK Constants.PROD
                                    ProductID = r.ExternalCode |> String.replace Constants.``GPK-`` ""
                                    ProductName = r.MedicationName
                                    MedicationName = r.MedicationName
                                    Manufacturer = Constants.Apotheek
                                    DoseForm = r.DoseForms
                                    Routes = r.Routes
                                    Format = r.Format
                                    IncrementValue = r.IncrementValue
//                                    Unit = "mL"
                                    DefaultUnit = r.Unit
                                    IsUnknownStrength = Constants.FALSE
                                    StrengthLEFT = r.ComplexMedications[0].Concentration
                                    StrengthLEFTUnit = r.ComplexMedications[0].ConcentrationUnit
                                    StrengthRIGHT = "1"
                                    StrengthRIGHTUnit = Constants.mL
                                    DiluentGroup = Constants.solveGroup
                                    ProductRequiresReconstitution = Constants.FALSE
                                    IsVolumeKnown = Constants.FALSE
                                    Volume = "0"
                                    DiluentName =
                                        if r.DoseForms |> String.contains Constants.emulsie then Constants.emulsie
                                        else Constants.``.``
                                    IsFormulary = r.IsFormulary
                                }
                            |]
                }
            )
            |> Array.filter (fun m ->
                includeAssort |> Array.isEmpty ||
                includeAssort
                |> Array.exists (fun a ->
                    m.Assortment |> Array.exists ((=) a) ||
                    a = UMCU && m.IsFormulary
                )
            )

        meds
        |> Array.filter (fun r -> r.Brands |> Array.isEmpty |> not)
        |> Array.collect (fun r ->
            r.Brands
            |> Array.map (fun b ->
                [|
                    "BrandName", b
                    "MedicationName", r.MedicationName
                |]
                |> mapBrand
            )
        )
        |> Array.append [| Data.brandHeadings |> String.concat "\t" |]
        |> createDataImport file newFile brandName

        meds
        |> Array.collect (fun r -> r.ComplexMedications)
        |> Array.map (fun r ->
            [|
                "ComplexMedicationName", r.ComplexMedictionName
                "IngredientName", r.IngredientName
                "Concentration", $"{r.Concentration |> Decimal.toStringNumberNLWithoutTrailingZeros}"
                "ConcentrationUnit", r.ConcentrationUnit
                "In", r.In
                "InUnit",r.InUnit
            |]
            |> mapComp
        )
        |> Array.append [| Data.complexMedicationHeadings |> String.concat "\t" |]
        |> createDataImport file newFile complName

        meds
        |> Array.collect (fun m -> m.Products)
        |> Array.map (fun p ->
            [|
                "ExternalCode", p.Id
                "ProductID", p.ProductID
                "ProductName", p.ProductName
                "MedicationName", p.MedicationName
                "Manufacturer", p.Manufacturer
                "DoseForm", p.DoseForm
                "Routes", p.Routes
                "Status", $"{Active}"
                "Format", p.Format
                "IncrementValue", p.IncrementValue |> Decimal.toStringNumberNLWithoutTrailingZeros
                "DefaultUnit", p.DefaultUnit
                "IsUnknownStrength", p.IsUnknownStrength
                "StrengthLEFT", p.StrengthLEFT |> Decimal.toStringNumberNLWithoutTrailingZeros
                "StrengthLEFTUnit", p.StrengthLEFTUnit
                "StrengthRIGHT", p.StrengthRIGHT
                "StrengthRIGHTUnit", p.StrengthRIGHTUnit
                "DiluentGroup", p.DiluentGroup
                "ProductRequiresReconstitution", p.ProductRequiresReconstitution
                "IsVolumeKnown", p.IsVolumeKnown
                "Volume", p.Volume
                "DiluentName", p.DiluentName
                "IsFormulary", $"{p.IsFormulary |> mapBool}"
            |]
            |> mapProd
        )
        |> Array.append [| Data.productHeadings |> String.concat "\t" |]
        |> createDataImport file newFile prodName

        meds
        |> Array.map (fun m ->
            [|
                "ExternalCode", m.ExternalCode
                "MedicationName", m.MedicationName
                "Unit", m.Unit
                "ATCCode", m.ATC
                "Status", $"{m.Status}"
                "Format", m.Format
                "IncrementValue", $"{m.IncrementValue |> Decimal.toStringNumberNLWithoutTrailingZeros}"
                "CodeSnippetName", m.CodeSnippetName
                "Frequencies", m.Frequencies
                "DoseForms", m.DoseForms
                "Routes", m.Routes
                "AdditivesGroup", m.AdditivesGroup
                "DiluentsGroup", m.DiluentsGroup
                "DrugInDiluentGroup", m.DrugInDiluentGroup
                "DrugFamily", m.DrugFamily
                "DrugSubfamily", m.DrugSubfamily
                "IsFormulary", $"{m.IsFormulary |> mapBool}"

            |]
            |> mapMeds
        )
        |> Array.append (
            parentMeds
            |> Array.map mapMeds
        )
        |> Array.append (
            entFeeding
            |> Array.map mapMeds
        )
        |> Array.append [| Data.medicationHeadings |> String.concat "\t" |]
        |> createDataImport file newFile medName

        meds
        |> createTemplates file newFile


    let createSolutions file solName (meds : GenPresProduct[]) =
        // get solutions
        meds
        |> Array.collect (fun gpp -> gpp.GenericProducts)
        |> Array.filter (fun gp -> gp.Id |> getFormulary |> Array.isEmpty |> not)
        |> Array.filter (fun gp -> gp.Substances |> Array.length <= 4)
        |> Array.filter (fun gp ->
            Data.includeSols |> Array.exists (fun s -> gp.Name |> String.toLower |> String.contains s)
        )
        |> Array.filter (fun gp ->
            let su = gp.Substances[0].ShapeUnit
            gp.Shape |> shapeInDiluent "" su ||
            gp.Shape |> shapeIsSolution "" su
        )
        |> Array.sortBy (fun gp -> gp.Name, gp.Shape, gp.Route)
        |> Array.collect (fun gp ->
            let mapSols = (Array.mapStringHeadings Data.solutionHeadings) >> (String.concat "\t")

            gp.Route
            |> Array.collect (fun r -> r |> String.splitAt ',')
            |> Array.map mapRoute
            |> Array.filter (String.isNullOrWhiteSpace >> not)
            |> Array.filter (fun r -> r = Constants.iv || r = Constants.im)
            |> Array.collect (fun r ->
                [|
                    Constants.ICC
                    Constants.ICK
                    Constants.NEO
                |]
                |> Array.map (fun dep ->
                    let su = gp.Substances[0].ShapeUnit
                    {|
                        GPK = gp.Id
                        Generic = gp.Name |> String.toLower |> String.trim
                        Shape = gp.Shape |> String.trim |> String.toLower
                        Route = r
                        Department = dep
                        DiluentVol =
                            if gp.Shape |> shapeInDiluent r su then "1" else "0"
                        Solutions = "NaCl;gluc5;gluc10"
                        Substance =
                            gp.Substances[0].SubstanceName
                            |> String.toLower
                            |> String.trim
                    |}
                )
                |> Array.map (fun r ->
                    [|
                        "GPK", $"%i{r.GPK}"
                        "Generic", r.Generic
                        "Shape", r.Shape
                        "Route", r.Route
                        "Dep", r.Department
                        "DiluentVol", r.DiluentVol
                        "Solutions", r.Solutions
                        "Substance", r.Substance
                    |]
                    |> mapSols
                )
            )
        )
        |> Array.append [| Data.solutionHeadings |> String.concat "\t" |]


    let insertClassification () =
        """
if not exists (select * from dbo.Orders_ClassificationSystem where ClassificationSystemName = 'Oplossen')
insert into dbo.Orders_ClassificationSystem (ClassificationSystemName, ClassificationSystemType, ClassificationSystemStatus) values ('Oplossen', 3, 0)

if not exists (select * from dbo.Orders_ClassificationSystem where ClassificationSystemName = 'Verdunnen')
insert into dbo.Orders_ClassificationSystem (ClassificationSystemName, ClassificationSystemType, ClassificationSystemStatus) values ('Verdunnen', 3, 0)

if not exists (select * from dbo.Orders_ClassificationSystem where ClassificationSystemName = 'Voeding')
insert into dbo.Orders_ClassificationSystem (ClassificationSystemName, ClassificationSystemType, ClassificationSystemStatus) values ('Voeding', 3, 0)

if not exists (select * from dbo.Orders_ClassificationSystem where ClassificationSystemName = 'Poeders')
insert into dbo.Orders_ClassificationSystem (ClassificationSystemName, ClassificationSystemType, ClassificationSystemStatus) values ('Poeders', 2, 0)
"""


    let insertDrugFamilies () =
        ATCGroup.get ()
        |> Array.map (fun g -> drugFamilyName g.ATC1 g.AnatomicalGroup)
        |> Array.distinct
        |> Array.sort
        |> Array.map (fun s ->
            $"""
if not exists (select * from dbo.DrugFamilies where [Name] = '{s}')
insert into dbo.DrugFamilies ([Name], IsHiddenInAllergies) values ('{s}', 0)
"""
        )
        |> String.concat "\n"
        |> fun sql ->
            ATCGroup.get ()
            |> Array.map (fun g ->
                drugFamilyName g.ATC1 g.AnatomicalGroup,
                drugFamilyName g.ATC3 g.TherapeuticSubGroup
            )
            |> Array.distinct
            |> Array.sort
            |> Array.map (fun (m, s) ->
                $"""
if not exists (select * from dbo.DrugSubFamilies where [Name] = '{s}')
insert into dbo.DrugSubFamilies (DrugFamilyID, [Name], IsHiddenInAllergies) values ((select top 1 ID from dbo.DrugFamilies where [Name] = '{m}'), '{s}', 0)
"""
            )
            |> String.concat "\n"
            |> fun s -> $"begin transaction\n{sql}\n\n{s}\nrollback"


    let insertAdditionalIngredients () =
        let declare = """
declare @MedId as int
declare @CompId as int
declare @ratio as decimal(38, 14)
declare @UnitID as smallint
declare @SortOrder as smallint
declare @InRatio as decimal(38, 14)
declare @InUnitID as smallint
declare @ClassId as smallint
declare @ClassOrder as int
"""

        let insert = """
if not exists(select * from dbo.Orders_MedicationNonActiveComponents mc where mc.MedicationID = @MedId and mc.ComponentID = @CompId)
insert into dbo.Orders_MedicationNonActiveComponents (MedicationID, ComponentID, Ratio, UnitID, SortOrder, InRatio, InUnitID, ObjectType) values (@MedId, @CompId, @ratio, @UnitID, @SortOrder, @InRatio, @InUnitID, 10)
"""

        let insertParent =
            mapParentMeds
            |> Array.skip 1
            |> Array.map (fun xs ->
                [|1..13|]
                |> Array.collect (fun indx ->
                    if xs[indx] |> Double.stringToFloat32 <= 0.f then [||]
                    else

                        let comp, unit =
                            match mapParentMeds[0][indx] |> String.splitAt ' ' with
                            | [|n;u|] -> n, u
                            | _ -> "", ""

                        $"""
set @MedId = (select p.ParameterId from dbo.Parameters p where p.ParameterName = '{xs[0]}')
set @CompId = (select p.ParameterId from dbo.Parameters p where p.ParameterName = '{comp}')
set @ratio = {xs[indx]}
set @UnitID = (select u.UnitID from dbo.Units u where u.UnitName = '{unit}')
set @SortOrder = {indx - 1}
set @InRatio = 1
set @InUnitID = (select u.UnitID from dbo.Units u where u.UnitName = 'mL')
"""
                        + insert
                        |> fun s -> [| s |]

                )
                |> Array.prepend [|
                if xs[14] = Constants.TRUE then
                    """
set @ClassId = (select ClassificationSystemID from dbo.Orders_ClassificationSystem where ClassificationSystemName = 'Oplossen')
set @ClassOrder = coalesce((select max(SortOrder) from dbo.Orders_ClassificationSystemMedications where ClassificationSystemID = @ClassId), -1) + 1

if not exists (select * from dbo.Orders_ClassificationSystemMedications where ClassificationSystemID = @ClassId and MedicationID = @MedId)
insert into dbo.Orders_ClassificationSystemMedications (ClassificationSystemID, MedicationID, SortOrder) values (@ClassId, @MedId, @Classorder)
"""
                if xs[15] = Constants.TRUE then
                    """
set @ClassId = (select ClassificationSystemID from dbo.Orders_ClassificationSystem where ClassificationSystemName = 'Verdunnen')
set @ClassOrder = coalesce((select max(SortOrder) from dbo.Orders_ClassificationSystemMedications where ClassificationSystemID = @ClassId), -1) + 1

if not exists (select * from dbo.Orders_ClassificationSystemMedications where ClassificationSystemID = @ClassId and MedicationID = @MedId)
insert into dbo.Orders_ClassificationSystemMedications (ClassificationSystemID, MedicationID, SortOrder) values (@ClassId, @MedId, @Classorder)
"""
                |]
                |> String.concat "\n"

            )
            |> String.concat "\n"


        let insertFeeding =
            mapEntFeeding
            |> Array.skip 1
            |> Array.map (fun xs ->
                [|2..14|]
                |> Array.collect (fun indx ->
                    if xs[indx] |> Double.stringToFloat32 <= 0.f then [||]
                    else

                        let comp, unit =
                            match mapEntFeeding[0][indx] |> String.splitAt ' ' with
                            | [|n;u|] -> n, u
                            | _ -> "", ""

                        $"""
set @MedId = (select p.ParameterId from dbo.Parameters p where p.ParameterName = '{xs[0]}')
set @CompId = (select p.ParameterId from dbo.Parameters p where p.ParameterName = '{comp}')
set @ratio = {xs[indx]}
set @UnitID = (select u.UnitID from dbo.Units u where u.UnitName = '{unit}')
set @SortOrder = {indx - 1}
set @InRatio = 1
set @InUnitID = (select u.UnitID from dbo.Units u where u.UnitName = '{xs[1]}')
"""
                        + insert
                        |> fun s -> [| s |]

                )
                |> Array.prepend [|
                let classN =
                    if xs[1] = Constants.g then Constants.Poeders else Constants.Voeding

                $"""
set @ClassId = (select ClassificationSystemID from dbo.Orders_ClassificationSystem where ClassificationSystemName = '{classN}')
set @ClassOrder = coalesce((select max(SortOrder) from dbo.Orders_ClassificationSystemMedications where ClassificationSystemID = @ClassId), -1) + 1

if not exists (select * from dbo.Orders_ClassificationSystemMedications where ClassificationSystemID = @ClassId and MedicationID = @MedId)
insert into dbo.Orders_ClassificationSystemMedications (ClassificationSystemID, MedicationID, SortOrder) values (@ClassId, @MedId, @Classorder)
"""
                |]

                |> String.concat "\n"
            )
            |> String.concat "\n"


        $"begin transaction\n{declare}\n{insertParent}\n\n{insertFeeding}rollback"


    let config =
        {
            Ingredients = "Ingredients"
            Medications = "Medications"
            ComplexMedications = "ComplexMedications"
            Brands = "Brands"
            Products = "Products"
            OrderTemplates = "OrderTemplates"
            ImportFile = "data/metavision/DrugDatabaseForImport.xlsx"
            IncludeAssortment = [||]
        }


    let createImport (config : ImportConfig) =
        let fstFile = "data/output/DrugDatabaseForImport First.xlsx"
        let sndFile = "data/output/DrugDatabaseForImport Second.xlsx"

        printfn "create drug families insert script"
        insertDrugFamilies()
        |> File.writeTextToFile "data/output/InsertDrugFamilies.sql"

        printfn "create classification script"
        insertClassification()
        |> File.writeTextToFile "data/output/InsertClassification.sql"

        printfn "create ingredients insert script"
        insertAdditionalIngredients()
        |> File.writeTextToFile "data/output/InsertIngredients.sql"

        printfn "creating routes"
        let rts =
            createRoutes config.ImportFile fstFile "Routes"

        printfn "creating dose forms"
        rts
        |> createDoseForms config.ImportFile fstFile "DoseForms"

        printfn "creating non G-Standard medications"
        Array.empty
        |> createMedications
            [||]
            config.ImportFile
            fstFile
            config.Ingredients
            config.Medications
            config.ComplexMedications
            config.Brands
            config.Products

        printfn "creating ATC medication groups"
        Data.scriptATCMedicationGroups
        |> File.writeTextToFile "data/output/InsertATCMedicationGroups.sql"

        printfn "creating G-Standard medications"
        createMedications
            config.IncludeAssortment
            config.ImportFile
            sndFile
            config.Ingredients
            config.Medications
            config.ComplexMedications
            config.Brands
            config.Products
//            config.OrderTemplates

