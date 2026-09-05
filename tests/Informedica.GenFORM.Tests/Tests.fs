namespace Informedica.GenForm.Tests


/// Create the necessary test generators
module Generators =


    open Expecto
    open FsCheck
    open Informedica.Utils.Lib.BCL

    let bigRGen (n, d) =
        let d = if d = 0 then 1 else d
        let n = abs n |> BigRational.FromInt
        let d = abs d |> BigRational.FromInt
        n / d


    let bigRGenOpt (n, _) = bigRGen (n, 1) |> Some


    let bigRGenerator =
        gen {
            let! n = Arb.generate<int>
            let! d = Arb.generate<int>
            return bigRGen (n, d)
        }


    type BigRGenerator() =
        static member BigRational() =
            { new Arbitrary<BigRational>() with
                override x.Generator = bigRGenerator
            }


    type MinMax = MinMax of BigRational * BigRational

    let minMaxArb () =
        bigRGenerator
        |> Gen.map abs
        |> Gen.two
        |> Gen.map (fun (br1, br2) ->
            let br1 = br1.Numerator |> BigRational.FromBigInt
            let br2 = br2.Numerator |> BigRational.FromBigInt

            if br1 >= br2 then br2, br1 else br1, br2
            |> fun (br1, br2) -> if br1 = br2 then br1, br2 + 1N else br1, br2
        )
        |> Arb.fromGen
        |> Arb.convert MinMax (fun (MinMax(min, max)) -> min, max)


    type ListOf37<'a> = ListOf37 of 'a List

    let listOf37Arb () =
        Gen.listOfLength 37 Arb.generate
        |> Arb.fromGen
        |> Arb.convert ListOf37 (fun (ListOf37 xs) -> xs)


    let config =
        { FsCheckConfig.defaultConfig with
            arbitrary =
                [
                    typeof<BigRGenerator>
                    typeof<ListOf37<_>>.DeclaringType
                    typeof<MinMax>.DeclaringType
                ]
                @ FsCheckConfig.defaultConfig.arbitrary
            maxTest = 1000
        }


    let testProp testName prop =
        prop |> testPropertyWithConfig config testName


module GenericLabelTests =


    open Expecto
    open Expecto.Flip
    open Informedica.GenForm.Lib


    /// Regression tests for the generic-name vs generic-label distinction.
    /// External lookups (G-Standaard / dose checking) must key on the base
    /// substance name, while selection/display uses the full label.
    let tests =
        testList
            "GenericLabel name vs label"
            [
                test "genericName strips the brand qualifier" {
                    GenericBrand("glycopyrronium", "Sialanar")
                    |> GenericLabel.genericName
                    |> Expect.equal "should be the base substance name" "glycopyrronium"
                }

                test "genericName strips the form qualifier" {
                    GenericForm("paracetamol", "tablet")
                    |> GenericLabel.genericName
                    |> Expect.equal "should be the base substance name" "paracetamol"
                }

                test "toString keeps the brand qualifier for display/selection" {
                    GenericBrand("glycopyrronium", "Sialanar")
                    |> GenericLabel.toString
                    |> Expect.equal "should be the full label" "glycopyrronium (Sialanar)"
                }

                test "Generic.genericName delegates to the label" {
                    Generic.create (GenericBrand("glycopyrronium", "Sialanar")) (Solid "drank") []
                    |> Generic.genericName
                    |> Expect.equal "should be the base substance name" "glycopyrronium"
                }

                test "shorthand and canonical names are returned as-is" {
                    Shorthand "amoxicilline"
                    |> GenericLabel.genericName
                    |> Expect.equal "shorthand is the name" "amoxicilline"

                    Canonical [ "amoxicilline"; "clavulaanzuur" ]
                    |> GenericLabel.genericName
                    |> Expect.equal "canonical joins with /" "amoxicilline/clavulaanzuur"
                }
            ]


module ProductFilterTests =


    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Expecto
    open Expecto.Flip
    open Informedica.GenForm.Lib


    /// Route mapping covering the routes used by the test fixtures.
    let routeMapping =
        [|
            {
                Long = "oraal"
                Short = "or"
            }
            {
                Long = "iv"
                Short = "iv"
            }
        |]


    /// Build a Substance with only its name set.
    let subst name : Substance =
        {
            Name = name
            Concentration = None
            MolarConcentration = None
        }


    let para = subst "paracetamol"
    let sorbitol = subst "sorbitol"
    let amox = subst "amoxicilline"


    /// Build a TradeProduct.
    let trade hpk brand substs : TradeProduct =
        {
            HPK = hpk
            Brand = brand
            Substances = substs
        }


    /// A ProductComponent with neutral defaults; only the fields that
    /// drive Product.filter are overridden per fixture.
    let baseProd: ProductComponent =
        {
            GPK = ""
            ATC = ""
            MainGroup = ""
            SubGroup = ""
            Generic = ""
            TallMan = ""
            Synonyms = [||]
            ProductLabels = []
            Label = ""
            Form = ""
            Routes = [||]
            FormQuantities = ValueUnit.singleWithUnit Units.Mass.milliGram 1N
            FormUnit = Units.Mass.milliGram
            RequiresReconstitution = false
            Reconstitution = [||]
            Divisible = None
            Substances = [||]
            TradeProducts = []
        }


    // paracetamol tablet, oraal, two brands
    let prodA =
        { baseProd with
            GPK = "1001"
            Generic = "paracetamol"
            Form = "tablet"
            Routes = [| "oraal" |]
            Substances = [| para |]
            TradeProducts =
                [
                    trade "H1" "BrandX" [ para ]
                    trade "H2" "BrandY" [ para ]
                ]
        }

    // paracetamol drank, oraal; carries sorbitol that the trade product does not
    let prodB =
        { baseProd with
            GPK = "1002"
            Generic = "paracetamol"
            Form = "drank"
            Routes = [| "oraal" |]
            Substances = [| para; sorbitol |]
            TradeProducts = [ trade "H3" "BrandZ" [ para ] ]
        }

    // amoxicilline poeder, iv — should never match a paracetamol/oraal filter
    let prodC =
        { baseProd with
            GPK = "2001"
            Generic = "amoxicilline"
            Form = "poeder"
            Routes = [| "iv" |]
            Substances = [| amox |]
            TradeProducts = [ trade "H9" "BrandQ" [ amox ] ]
        }

    let prods = [| prodA; prodB; prodC |]


    let tests =
        testList
            "Product.filter tests"
            [

                test "filter by required cmp name and route returns matching products" {
                    prods
                    |> Product.filter routeMapping "oraal" "paracetamol" "" "" [||] [||]
                    |> Array.map _.GPK
                    |> Array.sort
                    |> Expect.equal "should return both paracetamol oraal products" [| "1001"; "1002" |]
                }

                test "filter excludes products whose route does not match" {
                    prods
                    |> Product.filter routeMapping "iv" "paracetamol" "" "" [||] [||]
                    |> Expect.isEmpty "no paracetamol product has route iv"
                }

                test "filter excludes products whose cmp name does not match" {
                    prods
                    |> Product.filter routeMapping "iv" "amoxicilline" "" "" [||] [||]
                    |> Array.map _.GPK
                    |> Expect.equal "only the amoxicilline product matches" [| "2001" |]
                }

                test "form refines the selected set" {
                    prods
                    |> Product.filter routeMapping "oraal" "paracetamol" "tablet" "" [||] [||]
                    |> Array.map _.GPK
                    |> Expect.equal "only the tablet remains" [| "1001" |]
                }

                test "empty form does not refine the selected set" {
                    prods
                    |> Product.filter routeMapping "oraal" "paracetamol" "" "" [||] [||]
                    |> Array.length
                    |> Expect.equal "both forms still pass" 2
                }

                test "gpk list refines the selected set" {
                    prods
                    |> Product.filter routeMapping "oraal" "paracetamol" "" "" [| "1002" |] [||]
                    |> Array.map _.GPK
                    |> Expect.equal "only the listed gpk remains" [| "1002" |]
                }

                test "brand refines to products carrying that trade product" {
                    let result =
                        prods |> Product.filter routeMapping "oraal" "paracetamol" "" "BrandX" [||] [||]

                    result
                    |> Array.map _.GPK
                    |> Expect.equal "only the product with a BrandX trade product remains" [| "1001" |]

                    result[0].TradeProducts
                    |> List.map _.HPK
                    |> Expect.equal "trade products narrowed to the BrandX trade product" [ "H1" ]
                }

                test "hpk list refines and narrows substances to the trade product" {
                    let result =
                        prods |> Product.filter routeMapping "oraal" "paracetamol" "" "" [||] [| "H3" |]

                    result
                    |> Array.map _.GPK
                    |> Expect.equal "only the product carrying HPK H3 remains" [| "1002" |]

                    result[0].TradeProducts
                    |> List.map _.HPK
                    |> Expect.equal "trade products narrowed to H3" [ "H3" ]

                    result[0].Substances
                    |> Array.map _.Name
                    |> Expect.equal "substances narrowed to those carried by H3" [| "paracetamol" |]
                }

                test "without brand or hpk the substances are not narrowed" {
                    let result =
                        prods
                        |> Product.filter routeMapping "oraal" "paracetamol" "" "" [||] [||]
                        |> Array.find (fun p -> p.GPK = "1002")

                    result.Substances
                    |> Array.map _.Name
                    |> Expect.equal "all substances of the product kept" [| "paracetamol"; "sorbitol" |]
                }
            ]


module DoseRuleProductTests =


    open Expecto
    open Expecto.Flip
    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib


    /// String non-empty (avoids depending on a BCL open in this module).
    let private ne s =
        s |> System.String.IsNullOrWhiteSpace |> not


    module PT = ProductFilterTests

    let private bp = PT.baseProd
    let private sCita = PT.subst "citalopram"
    let private sBup = PT.subst "bupropion"
    let private sAdr = PT.subst "adrenaline"
    let private sEth = PT.subst "ethanol"


    /// Route mapping covering the routes used by the fixtures.
    let routeMapping: RouteMapping[] =
        [|
            {
                Long = "ORAAL"
                Short = "or"
            }
            {
                Long = "INTRAMUSCULAIR"
                Short = "im"
            }
        |]


    /// Minimal product set reproducing the real attachment behaviour:
    /// two citalopram forms, two bupropion brands (one shared, one Wellbutrin-only),
    /// three adrenaline injection products (two listed, one not).
    let prods: ProductComponent[] =
        [|
            { bp with
                GPK = "182729"
                Generic = "citalopram"
                Form = "tablet"
                Routes = [| "ORAAL" |]
                Substances = [| sCita |]
                TradeProducts = [ PT.trade "2905779" "" [ sCita ] ]
            }
            { bp with
                GPK = "106496"
                Generic = "citalopram"
                Form = "druppels voor oraal gebruik"
                Routes = [| "ORAAL" |]
                Substances = [| sEth; sCita |]
                TradeProducts = [ PT.trade "1165933" "CIPRAMIL" [ sCita ] ]
            }
            { bp with
                GPK = "109347"
                Generic = "bupropion"
                Form = "tablet met gereguleerde afgifte"
                Routes = [| "ORAAL" |]
                Substances = [| sBup |]
                TradeProducts =
                    [
                        PT.trade "1181572" "ZYBAN" [ sBup ]
                        PT.trade "1848437" "WELLBUTRIN" [ sBup ]
                    ]
            }
            { bp with
                GPK = "126969"
                Generic = "bupropion"
                Form = "tablet met gereguleerde afgifte"
                Routes = [| "ORAAL" |]
                Substances = [| sBup |]
                TradeProducts = [ PT.trade "1848445" "WELLBUTRIN" [ sBup ] ]
            }
            { bp with
                GPK = "170925"
                Generic = "adrenaline"
                Form = "injectievloeistof"
                Routes = [| "INTRAMUSCULAIR"; "INTRAVENEUS" |]
                Substances = [| sAdr |]
                TradeProducts = [ PT.trade "786993" "EPIPEN" [ sAdr ] ]
            }
            { bp with
                GPK = "170933"
                Generic = "adrenaline"
                Form = "injectievloeistof"
                Routes = [| "INTRAMUSCULAIR" |]
                Substances = [| sAdr |]
                TradeProducts = [ PT.trade "787000" "EPIPEN JUNIOR" [ sAdr ] ]
            }
            { bp with
                GPK = "170976"
                Generic = "adrenaline"
                Form = "injectievloeistof"
                Routes = [| "INTRAMUSCULAIR"; "INTRAVENEUS" |]
                Substances = [| sAdr |]
                TradeProducts = [ PT.trade "2590654" "" [ sAdr ] ]
            }
        |]


    // Neutral DoseRuleData defaults; mkData overrides only the narrowing fields.
    let private emptyGen: GenericData =
        {
            Name = ""
            Form = ""
            Brand = ""
            GPKs = [||]
            HPKs = [||]
        }

    let private emptyDL: DoseLimitData =
        {
            CmpBased = false
            Component = ""
            Substance = ""
            DoseUnit = ""
            MinQty = None
            MaxQty = None
            MinQtyAdj = None
            MaxQtyAdj = None
            MinPerTime = None
            MaxPerTime = None
            MinPerTimeAdj = None
            MaxPerTimeAdj = None
            MinRate = None
            MaxRate = None
            MinRateAdj = None
            MaxRateAdj = None
        }

    let private emptySched: ScheduleData =
        {
            // Once is always valid under validateData, so each fixture
            // survives fromData's validation without schedule plumbing.
            DoseType = "once"
            DoseText = ""
            Freqs = [||]
            AdjustUnit = ""
            FreqUnit = ""
            RateUnit = ""
            MinTime = None
            MaxTime = None
            TimeUnit = ""
            MinInt = None
            MaxInt = None
            IntUnit = ""
            MinDur = None
            MaxDur = None
            DurUnit = ""
            DoseLimitData = emptyDL
        }

    let private emptyPat: PatientCategoryData =
        {
            Location = ""
            Dep = ""
            IsAdult = false
            Gender = AnyGender
            MinAge = None
            MaxAge = None
            MinWeight = None
            MaxWeight = None
            MinBSA = None
            MaxBSA = None
            MinGestAge = None
            MaxGestAge = None
            MinPMAge = None
            MaxPMAge = None
        }

    let private baseData: DoseRuleData =
        {
            RowId = ""
            RuleId = ""
            GrpId = ""
            SortNo = 1
            Source = "FTK"
            SourceText = ""
            Generic = emptyGen
            Indication = "test"
            Route = ""
            PatientText = ""
            Patient = emptyPat
            ScheduleText = ""
            ScheduleData = emptySched
            Validated = None
            FreqCheck = None
            DoseCheck = None
        }


    /// Build a DoseRuleData row with the given generic, route and narrowing.
    /// Component is set to the generic so products match by component name.
    /// A component dose value is set so the built rule carries a real limit and
    /// is not dropped by DoseRule.removeEmptyLimits (these fixtures exercise
    /// product attachment, not limits).
    let mkData gen rte form brand gpks hpks : DoseRuleData =
        { baseData with
            Route = rte
            Generic =
                { emptyGen with
                    Name = gen
                    Form = form
                    Brand = brand
                    GPKs = gpks
                    HPKs = hpks
                }
            ScheduleData =
                { emptySched with
                    DoseLimitData =
                        { emptyDL with
                            Component = gen
                            DoseUnit = "mg"
                            MaxQty = Some(BigRational.FromInt 100)
                        }
                }
        }


    /// Build the DoseRules for the given raw rows (empty FormRoutes is safe:
    /// addFormLimits only sets FormLimit, product attachment is unaffected).
    let buildRules (data: DoseRuleData[]) =
        DoseRuleLoader.fromData routeMapping [||] prods data |> fst


    /// Sorted, distinct GPKs attached to a set of DoseRules.
    let attachedGpks (rules: DoseRule[]) =
        rules
        |> Array.collect (fun r -> r.ComponentLimits |> Array.collect _.Products)
        |> Array.map _.GPK
        |> Array.filter ne
        |> Array.distinct
        |> Array.sort


    let builtGpks data = data |> buildRules |> attachedGpks


    /// Apply the production single-narrowing normaliser (as parseDoseRuleData does).
    let normalize (d: DoseRuleData) =
        { d with Generic = d.Generic |> DoseRuleData.withSingleNarrowing }


    let formRow = mkData "citalopram" "ORAAL" "tablet" "" [||] [||]
    let brandRow = mkData "bupropion" "ORAAL" "" "Zyban" [||] [||]

    let gpksRow =
        mkData "adrenaline" "INTRAMUSCULAIR" "" "" [| "170925"; "170933" |] [||]
    // Same gpks narrowing, plus a Form that would (if applied) exclude every
    // adrenaline injection product — proves Form is dropped when GPKs win.
    let gpksFormRow =
        mkData "adrenaline" "INTRAMUSCULAIR" "tablet" "" [| "170925"; "170933" |] [||]


    /// Build a (component, substance) row with an optional MaxQty dose value,
    /// with RowId/RuleId/GrpId hashed (as the build pipeline does before dedup).
    let mkRow cmp subst (maxQty: int option) =
        { baseData with
            Route = "ORAAL"
            Generic = { emptyGen with Name = cmp }
            ScheduleData =
                { emptySched with
                    DoseLimitData =
                        { emptyDL with
                            Component = cmp
                            Substance = subst
                            DoseUnit = "mg"
                            MaxQty = maxQty |> Option.map BigRational.FromInt
                        }
                }
        }
        |> DoseRuleData.setDataHashIds


    let tests =
        testList
            "DoseRule.fromData product attachment"
            [

                test "form narrowing attaches only the matching-form product" {
                    builtGpks [| formRow |]
                    |> Expect.equal "only the tablet GPK attaches (drank/druppels excluded)" [| "182729" |]
                }

                test "brand narrowing attaches only the branded product" {
                    let rules = buildRules [| brandRow |]

                    rules
                    |> attachedGpks
                    |> Expect.equal "only the Zyban-carrying GPK attaches" [| "109347" |]

                    rules
                    |> Array.collect (fun r -> r.ComponentLimits |> Array.collect _.Products)
                    |> Array.collect (_.TradeProducts >> List.toArray)
                    |> Array.map _.Brand
                    |> Array.distinct
                    |> Array.sort
                    |> Expect.equal "trade products narrowed to the Zyban brand" [| "ZYBAN" |]
                }

                test "gpks narrowing attaches exactly the listed gpks" {
                    builtGpks [| gpksRow |]
                    |> Expect.equal "the two listed GPKs attach (170976 excluded)" [| "170925"; "170933" |]
                }

                test "precedence: GPKs win over Form (Form is ignored)" {
                    let normalized = builtGpks [| normalize gpksFormRow |]

                    normalized
                    |> Expect.equal "normalised row attaches the GPK-narrowed products" [| "170925"; "170933" |]

                    // Without normalisation the conflicting Form would exclude all
                    // injection products: this contrast proves Form was dropped.
                    let raw = builtGpks [| gpksFormRow |]

                    raw |> Expect.isEmpty "raw (unnormalised) gpks+form row attaches nothing"

                    normalized |> Expect.notEqual "normalisation changes the outcome" raw
                }

                test "each narrowed row yields exactly one DoseRule with products" {
                    for row in [| formRow; brandRow; gpksRow |] do
                        let rules = buildRules [| row |]

                        rules |> Array.length |> Expect.equal "one DoseRule built" 1

                        rules[0].ComponentLimits
                        |> Array.collect _.Products
                        |> Array.isEmpty
                        |> Expect.isFalse "the DoseRule carries attached products"
                }

                // Regression (PR #361 GenFORM v2): a component-based combination
                // dose rule must keep its combination generic label, not be
                // collapsed onto its single marker substance. Otherwise the
                // combination (e.g. amoxicilline/clavulaanzuur) is filed under the
                // marker generic (amoxicilline) and becomes un-prescribable.
                test "combination generic label is preserved (not collapsed to marker substance)" {
                    let comboRow =
                        { baseData with
                            Route = "ORAAL"
                            Generic = { emptyGen with Name = "amoxicilline/clavulaanzuur" }
                            ScheduleData =
                                { emptySched with
                                    DoseLimitData =
                                        { emptyDL with
                                            Component = "amoxicilline/clavulaanzuur"
                                            Substance = "amoxicilline"
                                            DoseUnit = "mg"
                                            MaxQty = Some(BigRational.FromInt 500)
                                        }
                                }
                        }

                    let rules = buildRules [| comboRow |]

                    rules |> Array.length |> Expect.equal "one DoseRule built" 1

                    rules[0].Generic
                    |> Generic.toString
                    |> Expect.equal "label stays the combination, not the marker substance" "amoxicilline/clavulaanzuur"
                }

                // Regression (PR #361 GenFORM v2): the substance rows of one
                // component must collapse into a single DoseRule with one
                // component carrying every substance limit. Rows are grouped by
                // semantic identity (hashId), never by DataId/GroupId/SortNo,
                // which are source-row bookkeeping that may be unset or differ
                // between substance rows. Otherwise amoxicilline/clavulaanzuur
                // splits into separate single-substance rules.
                test "combination substances collapse into one component (not split by DataId/SortNo)" {
                    let mk subst sortNo dataId =
                        { baseData with
                            RuleId = dataId
                            SortNo = sortNo
                            Route = "ORAAL"
                            Generic = { emptyGen with Name = "amoxicilline/clavulaanzuur" }
                            ScheduleData =
                                { emptySched with
                                    DoseLimitData =
                                        { emptyDL with
                                            Component = "amoxicilline/clavulaanzuur"
                                            Substance = subst
                                            DoseUnit = "mg"
                                            MaxQty = Some(BigRational.FromInt 250)
                                        }
                                }
                        }

                    // distinct DataId + SortNo per substance row, as the source data may carry
                    let rules = buildRules [| mk "amoxicilline" 1 "A"; mk "clavulaanzuur" 2 "B" |]

                    rules
                    |> Array.length
                    |> Expect.equal "substance rows collapse into one DoseRule" 1

                    rules[0].ComponentLimits
                    |> Array.length
                    |> Expect.equal "one component, not split per substance" 1

                    rules[0].ComponentLimits[0].SubstanceLimits
                    |> Array.map (_.DoseLimitTarget >> LimitTarget.toString)
                    |> Array.sort
                    |> Expect.equal "both substances live in the one component" [| "amoxicilline"; "clavulaanzuur" |]
                }

                // --- unit tests for the fromData build helpers ---

                test "dataGroupKey groups by selection key" {
                    let r1 = mkData "citalopram" "ORAAL" "tablet" "" [||] [||]
                    let r3 = mkData "citalopram" "INTRAMUSCULAIR" "tablet" "" [||] [||]

                    [| r1; r1; r3 |]
                    |> Array.groupBy DoseRuleData.dataGroupKey
                    |> Array.length
                    |> Expect.equal "same key merges, different route splits" 2
                }

                test "candidateProducts keeps only products matching a group component" {
                    DoseRuleLoader.candidateProducts prods [| mkData "citalopram" "ORAAL" "" "" [||] [||] |]
                    |> Array.map _.Generic
                    |> Array.distinct
                    |> Array.sort
                    |> Expect.equal "only citalopram products" [| "citalopram" |]
                }

                // An unnarrowed row expands to one DoseRule per pharmaceutical
                // form, each carrying that form's product (the form fan-out that
                // the old expandRowByFormAndUnitGroup tested, now driven through
                // the public fromData build).
                test "unnarrowed row expands to one DoseRule per pharmaceutical form" {
                    buildRules [| mkData "citalopram" "ORAAL" "" "" [||] [||] |]
                    |> Array.map (fun r ->
                        r.ComponentLimits |> Array.collect _.Products |> Array.map _.GPK |> Array.sort
                    )
                    |> Array.sortBy (Array.tryHead >> Option.defaultValue "")
                    |> Expect.equal
                        "one DoseRule per form, each with that form's product"
                        [| [| "106496" |]; [| "182729" |] |]
                }

                // A narrowing that matches no product still yields a DoseRule, but
                // built on a placeholder product carrying no real GPK. (The new
                // pipeline does not emit a no-products warning; that diagnostic is
                // a deferred follow-up.)
                test "no matching products yields a placeholder DoseRule (no real product attached)" {
                    let rules = buildRules [| mkData "citalopram" "ORAAL" "" "" [| "999999" |] [||] |]

                    rules |> Array.length |> Expect.equal "one placeholder DoseRule built" 1

                    rules
                    |> Array.collect (fun r -> r.ComponentLimits |> Array.collect _.Products)
                    |> Array.map _.GPK
                    |> Array.filter ne
                    |> Expect.isEmpty "placeholder carries no real GPK"
                }

                // --- RowId dedup (DoseRule.dedupRowsByRowId) ---

                test "identical duplicate (same comp+subst+values) collapses to one row, no warning" {
                    let rows =
                        [|
                            mkRow "amoxicilline" "amoxicilline" (Some 500)
                            mkRow "amoxicilline" "amoxicilline" (Some 500)
                        |]

                    let deduped, warns = rows |> DoseRuleData.dedupRowsByRowId

                    deduped |> Array.length |> Expect.equal "collapses to one row" 1
                    warns |> Expect.isEmpty "no warning for identical duplicate"
                }

                test "duplicate with differing dose values collapses to one row and warns" {
                    let rows =
                        [|
                            mkRow "amoxicilline" "amoxicilline" (Some 500)
                            mkRow "amoxicilline" "amoxicilline" (Some 750)
                        |]

                    let deduped, warns = rows |> DoseRuleData.dedupRowsByRowId

                    deduped |> Array.length |> Expect.equal "collapses to one row" 1

                    deduped[0].ScheduleData.DoseLimitData.MaxQty
                    |> Expect.equal "keeps the first occurrence (MaxQty 500)" (Some(BigRational.FromInt 500))

                    warns |> List.length |> Expect.equal "exactly one conflict warning" 1
                }

                test "keep-first is by input order: reversing the rows keeps the other value" {
                    // Pins the (intentional) order-dependence of dedupRowsByRowId:
                    // the survivor is the first row by position, NOT e.g. the
                    // smallest/strictest value. Reversing the input flips which
                    // dose-limit value is retained, and still emits one warning.
                    let rows =
                        [|
                            mkRow "amoxicilline" "amoxicilline" (Some 750)
                            mkRow "amoxicilline" "amoxicilline" (Some 500)
                        |]

                    let deduped, warns = rows |> DoseRuleData.dedupRowsByRowId

                    deduped |> Array.length |> Expect.equal "collapses to one row" 1

                    deduped[0].ScheduleData.DoseLimitData.MaxQty
                    |> Expect.equal
                        "keeps the first occurrence (MaxQty 750), not the smaller value"
                        (Some(BigRational.FromInt 750))

                    warns |> List.length |> Expect.equal "exactly one conflict warning" 1
                }

                test "distinct substances in the same component are kept as separate rows" {
                    let rows =
                        [|
                            mkRow "co-amoxiclav" "amoxicilline" (Some 500)
                            mkRow "co-amoxiclav" "clavulaanzuur" (Some 125)
                        |]

                    let deduped, warns = rows |> DoseRuleData.dedupRowsByRowId

                    deduped |> Array.length |> Expect.equal "both substances kept" 2
                    warns |> Expect.isEmpty "no warning for distinct substances"
                }

                test "RowId is equal for same (component, substance) and differs across substances" {
                    let a = mkRow "co-amoxiclav" "amoxicilline" (Some 500)
                    let b = mkRow "co-amoxiclav" "amoxicilline" (Some 750)
                    let c = mkRow "co-amoxiclav" "clavulaanzuur" (Some 125)

                    a.RowId |> Expect.equal "same (comp,subst) => equal RowId" b.RowId

                    (a.RowId = c.RowId) |> Expect.isFalse "different substance => differing RowId"
                }

                // --- empty-dose-rule removal (DoseRule.removeEmptyLimits) ---
                // A row that PASSES validateData (Once needs no schedule plumbing)
                // but carries NO dose values yields a dose rule whose only limit is
                // empty — no dosing meaning. fromData must drop it.

                test "fromData drops a dose rule whose only limit is empty" {
                    buildRules [| mkRow "citalopram" "citalopram" None |]
                    |> Expect.isEmpty "an all-empty-limit row produces no dose rule"
                }

                test "fromData keeps a dose rule that carries a real limit" {
                    let rules = buildRules [| mkRow "citalopram" "citalopram" (Some 100) |]

                    rules
                    |> Array.isEmpty
                    |> Expect.isFalse "the row with a dose value yields a rule"

                    rules
                    |> Array.forall (fun dr ->
                        dr.ComponentLimits
                        |> Array.exists (fun cl -> cl.SubstanceLimits |> Array.exists (DoseLimit.hasNoLimits >> not))
                    )
                    |> Expect.isTrue "every surviving rule carries a non-empty substance limit"
                }
            ]


module DoseRuleToDataTests =


    open Expecto
    open Expecto.Flip
    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib


    module DP = DoseRuleProductTests


    /// A component-only row with no dose limit reverses to nothing (the forward
    /// path builds a ComponentLimit with Limit = None). The narrowing fixtures
    /// therefore carry a substance so a SubstanceLimit — and hence a reversed
    /// row — is produced.
    let private withSubst subst (d: DoseRuleData) : DoseRuleData =
        { d with DoseRuleData.ScheduleData.DoseLimitData.Substance = subst }


    /// Forward (fromData) then reverse (DoseRule.toData) the given raw rows.
    /// This is the structural half of the round-trip: it exercises that toData
    /// recovers the categorical identity and the form/brand/gpks narrowing.
    /// (The full quantitative round-trip runs against live data in
    /// Scratch/Informedica.GenForm.Lib.fsx.)
    let private roundTrip (data: DoseRuleData[]) =
        data |> DP.buildRules |> Array.collect DoseRule.toData


    /// <summary>
    /// Declares which spreadsheet columns each sheet parser reads, and fails when a
    /// parser stops agreeing with its declaration. These tests, not a document, are
    /// the specification of the sheet contract.
    /// </summary>
    /// <remarks>
    /// <c>Csv.getColumn</c> RAISES on a column that is not in the header row, and
    /// every sheet parser wraps its body in try/with -> Error. So two assertions
    /// fix a declared column set exactly:
    ///
    /// 1. parsing a sheet whose header row is EXACTLY the declared set succeeds
    ///    => the parser reads no column outside the declared set;
    /// 2. dropping any single declared column makes the parse fail
    ///    => the parser really reads every declared column.
    ///
    /// Columns that may legitimately be absent are listed in <c>tolerated</c> and
    /// are exempt from (2) only; that list is the optionality contract.
    ///
    /// A failure means the parser and the declared column set disagree. Fix
    /// whichever is wrong - do NOT relax the test. If a column genuinely became
    /// optional, move it to <c>tolerated</c> and make the parser read it through a
    /// tolerant reader.
    ///
    /// Verified by mutation: renaming <c>get "MinQty"</c> to <c>get "MinQuantity"</c>
    /// in SolutionRule.fs turns assertion (1) for SolutionRules red. Note it is (1)
    /// that catches a rename - (2) stays green because the parse then fails for the
    /// wrong reason - so do not weaken (1) into "parses with at least these columns".
    /// </remarks>
    module ColumnContract =


        /// A sheet with the given header row and ONE data row. Cells are empty
        /// unless <c>overrides</c> gives a value: every parser must cope with an
        /// empty cell, so an empty row keeps each fixture down to the columns that
        /// must carry a value for the parser to reach the rest.
        let mkSheet (overrides: (string * string) list) (columns: string list) =
            let row =
                columns
                |> List.map (fun c ->
                    overrides
                    |> List.tryFind (fst >> String.equalsCapInsens c)
                    |> Option.map snd
                    |> Option.defaultValue ""
                )

            [| columns |> List.toArray; row |> List.toArray |]


        /// The unit mapping the FormRoute fixture needs: without a resolvable dose
        /// unit that parser short-circuits and never reads its dose columns.
        let unitMappingFixture =
            [|
                {
                    Long = "milligram"
                    Short = "mg"
                    MV = "mg"
                    Group = "Mass"
                }
            |]


        /// The DoseRules columns, taken from the ONE production list so the two
        /// cannot drift. <c>headers</c> is a single tab-joined line.
        let doseRuleColumns =
            let fromHeaders =
                DoseRuleData.headers |> List.head |> String.split "\t" |> List.map String.trim

            // "Loc" is read by the parser but missing from `headers` - see the TODO
            // there. Declared here because the sheet does carry it.
            fromHeaders @ [ "Loc" ]


        /// name, declared columns, columns that may be absent, empty-cell
        /// overrides, and the parser under test reduced to a success flag.
        let sheets: (string * string list * string list * (string * string) list * (string[][] -> bool)) list =
            [
                "Routes",
                [ "ZIndex"; "ShortDutch" ],
                [],
                [],
                (Mapping.parseSheet Mapping.routeMappingRow >> Result.isOk)

                "Units",
                [ "ZIndexUnitLong"; "Unit"; "MetaVisionUnit"; "Group" ],
                [],
                [],
                (Mapping.parseSheet Mapping.unitMappingRow >> Result.isOk)

                "ValidForms", [ "Form" ], [], [], (Mapping.parseSheet Mapping.validFormRow >> Result.isOk)

                "FormRoute",
                [
                    "Route"
                    "Form"
                    "Unit"
                    "DoseUnit"
                    "MinDoseQty"
                    "MaxDoseQty"
                    "MinDoseQtyKg"
                    "MaxDoseQtyKg"
                    "Divisible"
                    "Timed"
                    "Reconstitute"
                    "IsSolution"
                ],
                [],
                [ "Unit", "mg"; "DoseUnit", "mg" ],
                (Mapping.parseSheet (Mapping.formRouteRow unitMappingFixture) >> Result.isOk)

                "Totals",
                [
                    "Name"
                    "MinAge"
                    "MaxAge"
                    "MinWeight"
                    "MaxWeight"
                    "Unit"
                    "Adj"
                    "TimeUnit"
                    "MinPerTime"
                    "MaxPerTime"
                    "MinPerTimeAdj"
                    "MaxPerTimeAdj"
                ],
                [],
                [],
                (Mapping.parseSheet Mapping.totalsRow >> Result.isOk)

                "Reconstitution",
                [
                    "GPK"
                    "Route"
                    "Loc"
                    "Dep"
                    "DiluentVol"
                    "ExpansionVol"
                    "Diluents"
                ],
                [],
                [],
                (Product.Reconstitution.parseReconstitution >> Result.isOk)

                "Formulary",
                [
                    "GPKODE"
                    "Type"
                    "UMCU"
                    "ICC"
                    "NEO"
                    "ICK"
                    "HCK"
                    "Generic"
                    "TallMan"
                    "Divisible"
                    "UseGenName"
                    "Mmol"
                    "Form"
                    "Brand"
                    "GenName"
                    "GStand"
                    "Unit"
                    "Energy kCal"
                    "Carb g"
                    "Prot g"
                    "Lip g"
                    "Sod mmol"
                    "Pot mmol"
                    "Calc mmol"
                    "Posph mmol"
                    "Magn mmol"
                    "Chlor mmol"
                    "Iron mmol"
                    "VitD IE"
                    "IsReconste"
                    "IsDilute"
                    "IsAdditive"
                ],
                [],
                [],
                (Product.parseFormularyProducts >> Result.isOk)

                "DoseRules",
                doseRuleColumns,
                // read through getIfNull / getOpt / getInt, so a sheet may omit them
                [
                    "RowId"
                    "RuleId"
                    "GrpId"
                    "SortNo"
                    "SourceText"
                    "PatientText"
                    "ScheduleText"
                    "Loc"
                    "Validated"
                    "FreqCheck"
                    "DoseCheck"
                ],
                [],
                (DoseRuleData.parseDoseRuleData >> Result.isOk)

                "SolutionRules",
                [
                    "Generic"
                    "Form"
                    "Route"
                    "Indication"
                    "Loc"
                    "Dep"
                    "CVL"
                    "PVL"
                    "MinAge"
                    "MaxAge"
                    "MinWeight"
                    "MaxWeight"
                    "MinGestAge"
                    "MaxGestAge"
                    "MinDose"
                    "MaxDose"
                    "DoseType"
                    "DoseText"
                    "Solutions"
                    "Volumes"
                    "Div"
                    "MinVol"
                    "MaxVol"
                    "MinVolAdj"
                    "MaxVolAdj"
                    "MinPerc"
                    "MaxPerc"
                    "Component"
                    "Substance"
                    "Unit"
                    "Quantities"
                    "MinQty"
                    "MaxQty"
                    "MinQtyAdj"
                    "MaxQtyAdj"
                    "MinDrip"
                    "MaxDrip"
                    "MinConc"
                    "MaxConc"
                ],
                [],
                [],
                (SolutionRule.parseSolutionRuleData >> Result.isOk)

                "RenalRules",
                [
                    "Generic"
                    "Route"
                    "Indication"
                    "Source"
                    "MinAge"
                    "MaxAge"
                    "IntDial"
                    "ContDial"
                    "PerDial"
                    "MinGFR"
                    "MaxGFR"
                    "DoseType"
                    "DoseText"
                    "Freqs"
                    "DoseRed"
                    "DoseUnit"
                    "AdjustUnit"
                    "FreqUnit"
                    "RateUnit"
                    "MinInt"
                    "MaxInt"
                    "IntUnit"
                    "Substance"
                    "MinQty"
                    "MaxQty"
                    "NormQtyAdj"
                    "MinQtyAdj"
                    "MaxQtyAdj"
                    "MinPerTime"
                    "MaxPerTime"
                    "NormPerTimeAdj"
                    "MinPerTimeAdj"
                    "MaxPerTimeAdj"
                    "MinRate"
                    "MaxRate"
                    "MinRateAdj"
                    "MaxRateAdj"
                ],
                [],
                [],
                (RenalRule.parseRenalRuleData >> Result.isOk)
            ]


        let tests =
            testList
                "sheet column contract"
                [
                    for name, columns, tolerated, overrides, parseOk in sheets do
                        testList
                            name
                            [
                                test "the declared columns are sufficient to parse the sheet" {
                                    columns
                                    |> mkSheet overrides
                                    |> parseOk
                                    |> Expect.isTrue
                                        $"the %s{name} parser must not read any column outside its declared set"
                                }

                                for col in columns |> List.filter (fun c -> tolerated |> List.contains c |> not) do
                                    test $"the sheet cannot be parsed without column {col}" {
                                        columns
                                        |> List.filter (fun c -> c <> col)
                                        |> mkSheet overrides
                                        |> parseOk
                                        |> Expect.isFalse
                                            $"%s{name}.%s{col} is declared but never read - drop it or read it"
                                    }

                                for col in tolerated do
                                    test $"column {col} may be absent" {
                                        columns
                                        |> List.filter (fun c -> c <> col)
                                        |> mkSheet overrides
                                        |> parseOk
                                        |> Expect.isTrue
                                            $"%s{name}.%s{col} is declared optional but the parser requires it"
                                    }
                            ]
                ]


    let roundTripTests =
        testList
            "DoseRule.toData round-trip"
            [
                test "form narrowing is recovered from the generic label" {
                    let rev = roundTrip [| DP.formRow |> withSubst "citalopram" |]

                    rev
                    |> Array.exists (fun r ->
                        r.Generic.Name = "citalopram"
                        && r.Generic.Form = "tablet"
                        && r.Generic.Brand = ""
                        && r.Generic.GPKs |> Array.isEmpty
                    )
                    |> Expect.isTrue "a reversed row recovers Name + Form, with no brand/gpks"
                }

                test "brand narrowing is recovered from the generic label" {
                    let rev = roundTrip [| DP.brandRow |> withSubst "bupropion" |]

                    rev
                    |> Array.exists (fun r ->
                        r.Generic.Name = "bupropion"
                        && r.Generic.Brand |> String.equalsCapInsens "Zyban"
                        && r.Generic.Form = ""
                    )
                    |> Expect.isTrue "a reversed row recovers Name + Brand, with no form"
                }

                test "gpks narrowing is recovered from the products" {
                    let rev = roundTrip [| DP.gpksRow |> withSubst "adrenaline" |]

                    rev
                    |> Array.exists (fun r ->
                        r.Generic.Name = "adrenaline"
                        && (r.Generic.GPKs |> Array.sort) = [| "170925"; "170933" |]
                        && r.Generic.Form = ""
                        && r.Generic.Brand = ""
                    )
                    |> Expect.isTrue "a reversed row recovers the GPK narrowing, with no form/brand"
                }

                test "categorical fields are preserved on every reversed row" {
                    let rev = roundTrip [| DP.formRow |> withSubst "citalopram" |]

                    rev |> Array.isEmpty |> Expect.isFalse "produces at least one row"

                    rev
                    |> Array.forall (fun r ->
                        r.Route = "ORAAL"
                        && r.Indication = "test"
                        && r.Source = "FTK"
                        && r.ScheduleData.DoseType = "once"
                        && r.ScheduleData.DoseLimitData.Component = "citalopram"
                    )
                    |> Expect.isTrue "route/indication/source/dosetype/component are kept"
                }

                test "a substance limit reverses to a substance row (CmpBased = false)" {
                    let rev = roundTrip [| DP.mkRow "citalopram" "citalopram" (Some 20) |]

                    rev
                    |> Array.exists (fun r ->
                        let dl = r.ScheduleData.DoseLimitData
                        dl.Substance = "citalopram" && (dl.CmpBased |> not)
                    )
                    |> Expect.isTrue "reversed row carries the substance with CmpBased = false"
                }
            ]


    let tests = testList "DoseRule data" [ roundTripTests; ColumnContract.tests ]


/// Full DoseRule round-trip on OFFLINE fixtures (no network/cache/env).
/// Fixtures (doserules/routemappings/products .json) are generated once by
/// Scripts/DownloadFixtures.fsx from the DEMO data and committed; the .fsproj
/// copies them next to the test dll. forward = DoseRuleLoader.fromData (fr = [||],
/// since FormLimit is not part of the reverse); reverse = DoseRule.toData; the
/// comparison machinery mirrors Scratch/Informedica.GenForm.Lib.fsx (Analyse).
module DoseRuleRoundtripTests =


    open System
    open System.IO
    open Informedica.Utils.Lib.BCL
    open Expecto
    open Expecto.Flip
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenForm.Lib


    // BigRational fixture JSON lives in the shared FixtureJson module
    // (FixtureJson.fs), used by both this test and Scripts/DownloadFixtures.fsx.


    let private load<'T> name =
        Path.Combine(AppContext.BaseDirectory, "fixtures", name)
        |> File.ReadAllText
        |> FixtureJson.deSerialize<'T>

    // `lazy`, not plain values: a module-level binding runs in this module's static
    // constructor, i.e. during Expecto's test discovery. Anything that throws there
    // takes down the whole assembly, which then reports zero tests instead of one
    // failure — exactly how issue #523 hid a red build behind "0 failed". Lazy is
    // thread-safe by default, so each is still computed at most once even though
    // Expecto runs tests in parallel.
    let private data = lazy (load<DoseRuleData[]> "doserules.json")
    let private rm = lazy (load<RouteMapping[]> "routemappings.json")
    let private prods = lazy (load<ProductComponent[]> "products.json")

    // fr = [||]: fromData uses FormRoute only for FormLimit, which toData does not
    // emit and the round-trip does not compare (as DoseRuleProductTests do).
    let private forward (d: DoseRuleData[]) =
        DoseRuleLoader.fromData rm.Value [||] prods.Value d |> fst


    // ---- comparison machinery (mirrors the scratch Analyse module) ----
    let private unitStr (u: Unit) = u |> Units.toStringEngShortWithoutGroup

    let private brStr (br: BigRational option) =
        br |> Option.map _.ToString() |> Option.defaultValue ""

    let private genKey (g: GenericData) =
        [
            g.Name
            g.Form
            g.Brand
            g.GPKs |> Array.sort |> String.concat ","
            g.HPKs |> Array.sort |> String.concat ","
        ]
        |> String.concat "~"

    let private patKey (p: PatientCategoryData) =
        let minAge, maxAge = if p.IsAdult then "", "" else brStr p.MinAge, brStr p.MaxAge

        [
            p.Location
            p.Dep
            string p.IsAdult
            $"%A{p.Gender}"
            minAge
            maxAge
            brStr p.MinWeight
            brStr p.MaxWeight
            brStr p.MinBSA
            brStr p.MaxBSA
            brStr p.MinGestAge
            brStr p.MaxGestAge
            brStr p.MinPMAge
            brStr p.MaxPMAge
        ]
        |> String.concat "~"

    let private limTok (lim: Limit option) = lim |> Option.map Limit.toToken

    let private doseLeaves (d: DoseRuleData) : Map<string, string> =
        DoseRule.getDoseLimits [| d |]
        |> Array.collect (fun dl ->
            [|
                "qty.min", limTok dl.Quantity.Min
                "qty.max", limTok dl.Quantity.Max
                "qtyAdj.min", limTok dl.QuantityAdjust.Min
                "qtyAdj.max", limTok dl.QuantityAdjust.Max
                "perTime.min", limTok dl.PerTime.Min
                "perTime.max", limTok dl.PerTime.Max
                "perTimeAdj.min", limTok dl.PerTimeAdjust.Min
                "perTimeAdj.max", limTok dl.PerTimeAdjust.Max
                "rate.min", limTok dl.Rate.Min
                "rate.max", limTok dl.Rate.Max
                "rateAdj.min", limTok dl.RateAdjust.Min
                "rateAdj.max", limTok dl.RateAdjust.Max
            |]
        )
        |> Array.choose (fun (k, v) -> v |> Option.map (fun t -> k, t))
        |> Map.ofArray

    let private timeTok (b: BigRational option) (us: string) =
        match b, (us |> Utils.Units.timeUnit) with
        | Some v, Some u -> ValueUnit.singleWithUnit u v |> ValueUnit.toToken |> Some
        | Some v, None -> $"%s{string v} %s{us |> String.trim}" |> Some
        | None, _ -> None

    let private schedLeaves (d: DoseRuleData) : Map<string, string> =
        let s = d.ScheduleData

        [
            "admin.min", timeTok s.MinTime s.TimeUnit
            "admin.max", timeTok s.MaxTime s.TimeUnit
            "int.min", timeTok s.MinInt s.IntUnit
            "int.max", timeTok s.MaxInt s.IntUnit
            "dur.min", timeTok s.MinDur s.DurUnit
            "dur.max", timeTok s.MaxDur s.DurUnit
        ]
        |> List.choose (fun (k, v) -> v |> Option.map (fun t -> k, t))
        |> Map.ofList

    let private freqSet (d: DoseRuleData) : Set<string> =
        let s = d.ScheduleData

        match s.FreqUnit |> Utils.Units.freqUnit with
        | Some u ->
            s.Freqs
            |> Array.map (fun f -> [| f |] |> ValueUnit.withUnit u |> ValueUnit.toToken)
            |> Set.ofArray
        | None ->
            s.Freqs
            |> Array.map (fun f -> $"%s{string f}/%s{s.FreqUnit |> String.trim}")
            |> Set.ofArray

    let private idKey (d: DoseRuleData) =
        [
            d.Source
            d.SourceText
            d.Indication
            d.Route
            d.PatientText
            d.ScheduleText
            genKey d.Generic
            patKey d.Patient
            d.ScheduleData.DoseType |> String.toLower
            d.ScheduleData.DoseText
            d.ScheduleData.DoseLimitData.Component
            d.ScheduleData.DoseLimitData.Substance
            string d.ScheduleData.DoseLimitData.CmpBased
        ]
        |> String.concat "||"

    let private rowKey (d: DoseRuleData) =
        let m2s (m: Map<string, string>) =
            m |> Map.toList |> List.map (fun (k, v) -> k + "=" + v) |> String.concat ";"

        [
            idKey d
            doseLeaves d |> m2s
            schedLeaves d |> m2s
            freqSet d |> Set.toList |> String.concat ","
        ]
        |> String.concat "##"

    let private subMap (om: Map<string, string>) (gm: Map<string, string>) =
        om
        |> Map.forall (fun k v ->
            match gm.TryFind k with
            | Some gv -> gv = v
            | None -> false
        )

    let private containedIn (gen: DoseRuleData) (orig: DoseRuleData) =
        subMap (doseLeaves orig) (doseLeaves gen)
        && subMap (schedLeaves orig) (schedLeaves gen)
        && Set.isSubset (freqSet orig) (freqSet gen)

    let private allLimitsEmpty (d: DoseRuleData) =
        let l = d.ScheduleData.DoseLimitData

        [
            l.MinQty
            l.MaxQty
            l.MinQtyAdj
            l.MaxQtyAdj
            l.MinPerTime
            l.MaxPerTime
            l.MinPerTimeAdj
            l.MaxPerTimeAdj
            l.MinRate
            l.MaxRate
            l.MinRateAdj
            l.MaxRateAdj
        ]
        |> List.forall Option.isNone

    let private surviving (rows: DoseRuleData[]) =
        rows
        |> Array.filter (fun d -> DoseRuleData.validateData d |> List.isEmpty)
        |> Array.filter (fun d ->
            not (
                d.ScheduleData.DoseLimitData.Substance |> String.isNullOrWhiteSpace
                && allLimitsEmpty d
            )
        )

    /// reverse-of-forward, deduped
    let private generate (rows: DoseRuleData[]) =
        rows |> forward |> Array.collect DoseRule.toData |> Array.distinctBy rowKey

    /// input rows not contained in any same-identity generated row
    let private missing (input: DoseRuleData[]) (gen: DoseRuleData[]) =
        let byId = gen |> Array.groupBy idKey |> dict

        surviving input
        |> Array.filter (fun o ->
            match byId.TryGetValue(idKey o) with
            | true, gs -> gs |> Array.exists (fun g -> containedIn g o) |> not
            | _ -> true
        )

    // Computed once, on first use rather than at test-discovery time (see the note
    // on the fixture bindings above).
    let private g1 = lazy (generate data.Value)
    let private g2 = lazy (generate g1.Value)


    // NOTE: the literal counts below (206/52/299/202) are INTENTIONAL snapshot
    // values frozen against the committed fixtures (doserules/products .json).
    // They are a tripwire that flags unexpected drift in the source data, NOT
    // part of the round-trip invariant — that is carried entirely by the
    // `missing ... = 0` containment checks in PASS 1/PASS 2, which hold for any
    // fixture. If you regenerate the fixtures via Scripts/DownloadFixtures.fsx
    // (e.g. after a data-source update), update these counts to match; a count
    // change alone does not indicate a round-trip regression.
    let tests =
        testList
            "DoseRule round-trip (offline fixtures)"
            [
                test "fixtures load and forward builds rules" {
                    data.Value.Length |> Expect.equal "subset dose-rule rows" 206
                    prods.Value.Length |> Expect.equal "subset products" 52

                    (data.Value |> forward |> Array.length)
                    |> Expect.equal "PASS 1 forward rules" 299
                }

                test "PASS 1 round-trips the source-derived fixture (frozen counts)" {
                    g1.Value.Length |> Expect.equal "PASS 1 generated (distinct)" 202
                    (missing data.Value g1.Value).Length |> Expect.equal "PASS 1 missing" 0
                }

                test "PASS 2 is a 100% containment fixpoint" {
                    (missing g1.Value g2.Value).Length
                    |> Expect.equal "PASS 2 missing (every input contained)" 0
                }
            ]


module Tests =


    open Informedica.Utils.Lib.BCL
    open Expecto
    open Expecto.Flip

    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib


    module DoseLimitTests =

        open Informedica.Utils.Lib.BCL

        let tests =
            testList
                "Dose Limit to string tests"
                [

                    test "printMinMaxDose with empty MinMax returns empty string" {
                        let result = DoseLimit.printMinMaxDose "[qty]" "/dosis" MinMax.empty

                        result |> Expect.equal "should be empty" ""
                    }

                    test "printMinMaxDose with label and empty perDose" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                            }

                        let result = DoseLimit.printMinMaxDose "[rate]" "" minMax

                        result |> Expect.isNonEmpty "should contain value"

                        result |> _.Contains("[rate]") |> Expect.isTrue "should contain label"
                    }

                    test "printMinMaxDose with label and perDose suffix" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                            }

                        let result = DoseLimit.printMinMaxDose "[qty]" "/dosis" minMax

                        result |> _.Contains("/dosis") |> Expect.isTrue "should contain perDose suffix"

                        result |> _.Contains("[qty]") |> Expect.isTrue "should contain label"
                    }

                    test "printMinMaxDose with empty label uses decimal format" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                            }

                        let result = DoseLimit.printMinMaxDose "" "/dosis" minMax

                        result |> Expect.isNonEmpty "should contain value"

                        // When label is empty, uses range format "10 - 20 mg/dosis" (not "min"/"max" prefixes)
                        result
                        |> fun s ->
                            s.Contains("10")
                            && s.Contains("20")
                            && s.Contains("-")
                            && s.Contains("mg")
                            && s.Contains("/dosis")
                        |> Expect.isTrue "should contain range with values, unit and perDose suffix"
                    }

                    test "printMinMaxDose with norm dose (min equals max) returns single value" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 15N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 15N))
                            }

                        let result = DoseLimit.printMinMaxDose "[qty]" "/dosis" minMax

                        result |> Expect.isNonEmpty "should contain value"

                        // Should not contain "min" or "max" when it's a norm dose
                        result
                        |> String.toLower
                        |> fun s -> s.Contains("min") || s.Contains("max")
                        |> Expect.isFalse "should not contain min/max for norm dose"
                    }

                    test "toString with empty DoseLimit returns only target" {
                        let dl = DoseLimit.limit

                        let result = dl |> DoseLimit.toString

                        result |> Expect.isNonEmpty "should contain at least target"
                    }

                    test "toString with quantity includes [qty] label and /dosis suffix" {
                        let dl =
                            { DoseLimit.limit with
                                Quantity =
                                    {
                                        Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                        Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                                    }
                            }

                        let result = dl |> DoseLimit.toString |> List.head

                        result |> _.Contains("[qty]") |> Expect.isTrue "should contain [qty] label"

                        result |> _.Contains("/dosis") |> Expect.isTrue "should contain /dosis suffix"
                    }

                    test "toString with PerTime includes [per-time] label" {
                        let dl =
                            { DoseLimit.limit with
                                PerTime =
                                    {
                                        Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Time.day 1N))
                                        Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Time.day 2N))
                                    }
                            }

                        let result = dl |> DoseLimit.toString |> List.head

                        result
                        |> _.Contains("[per-time]")
                        |> Expect.isTrue "should contain [per-time] label"
                    }

                    test "toString with multiple fields includes all labels" {
                        let dl =
                            { DoseLimit.limit with
                                Quantity =
                                    {
                                        Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                        Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                                    }
                                PerTime =
                                    {
                                        Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Time.day 1N))
                                        Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Time.day 2N))
                                    }
                            }

                        let result = dl |> DoseLimit.toString |> List.head

                        result |> _.Contains("[qty]") |> Expect.isTrue "should contain [qty] label"

                        result
                        |> _.Contains("[per-time]")
                        |> Expect.isTrue "should contain [per-time] label"
                    }

                    test "toString with PerTimeAdjust includes adjust labels" {
                        let dl =
                            { DoseLimit.limit with
                                PerTimeAdjust =
                                    {
                                        Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Time.day 1N))
                                        Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Time.day 3N))
                                    }
                            }

                        let result = dl |> DoseLimit.toString |> List.head

                        result
                        |> _.Contains("[per-time-adj]")
                        |> Expect.isTrue "should contain [per-time-adj] label"
                    }

                    test "isNormDose returns true when min equals max" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 15N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 15N))
                            }

                        minMax |> DoseLimit.isNormDose |> Expect.isTrue "should be norm dose"
                    }

                    test "isNormDose returns false when min differs from max" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                            }

                        minMax |> DoseLimit.isNormDose |> Expect.isFalse "should not be norm dose"
                    }

                    test "getNormDose returns Some when min equals max" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 15N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 15N))
                            }

                        minMax |> DoseLimit.getNormDose |> Expect.isSome "should return Some"
                    }

                    test "getNormDose returns None when min differs from max" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                            }

                        minMax |> DoseLimit.getNormDose |> Expect.isNone "should return None"
                    }
                ]


    module AdjustDoseLimitTests =

        let mkLimit v u =
            Limit.Inclusive(ValueUnit.singleWithUnit u v)

        let mg = Units.Mass.milliGram
        let mgPerKg = Units.Mass.milliGram |> ValueUnit.per Units.Weight.kiloGram
        let kg = Units.Weight.kiloGram
        let day = Units.Time.day
        let mgPerDay = mg |> ValueUnit.per day
        let mgPerKgPerDay = mgPerKg |> ValueUnit.per day
        let perDay = Units.Count.times |> ValueUnit.per day

        let pat15kg =
            { Patient.patient with Weight = Some(ValueUnit.singleWithUnit kg 15N) }

        let pat24kg =
            { Patient.patient with Weight = Some(ValueUnit.singleWithUnit kg 24N) }

        /// A frequency ValueUnit set (per day) from a list of frequencies.
        let freqSet (vs: bigint list) =
            vs
            |> List.map BigRational.fromBigInt
            |> List.toArray
            |> ValueUnit.withUnit perDay

        let tests =
            testList
                "adjustDoseLimitToPatient tests"
                [
                    test "no change when adjust min * adj < absolute max" {
                        let dl =
                            { DoseLimit.limit with
                                AdjustUnit = Some kg
                                DoseUnit = mg
                                Quantity =
                                    {
                                        Min = None
                                        Max = Some(mkLimit 100N mg)
                                    }
                                QuantityAdjust =
                                    {
                                        Min = Some(mkLimit 1N mgPerKg)
                                        Max = None
                                    }
                            }

                        let result = dl |> PrescriptionRule.adjustDoseLimitToPatient None pat15kg

                        result.QuantityAdjust
                        |> Expect.notEqual "adjust should be preserved" MinMax.empty
                    }

                    test "pins to max when adjust min * adj >= absolute max" {
                        let dl =
                            { DoseLimit.limit with
                                AdjustUnit = Some kg
                                DoseUnit = mg
                                Quantity =
                                    {
                                        Min = None
                                        Max = Some(mkLimit 10N mg)
                                    }
                                QuantityAdjust =
                                    {
                                        Min = Some(mkLimit 1N mgPerKg)
                                        Max = None
                                    }
                            }

                        let result = dl |> PrescriptionRule.adjustDoseLimitToPatient None pat15kg

                        result.QuantityAdjust |> Expect.equal "adjust should be cleared" MinMax.empty

                        result.Quantity.Min
                        |> Expect.equal "min should be pinned to max" result.Quantity.Max
                    }

                    test "no change when adjust max * adj > absolute min" {
                        let dl =
                            { DoseLimit.limit with
                                AdjustUnit = Some kg
                                DoseUnit = mg
                                Quantity =
                                    {
                                        Min = Some(mkLimit 10N mg)
                                        Max = None
                                    }
                                QuantityAdjust =
                                    {
                                        Min = None
                                        Max = Some(mkLimit 1N mgPerKg)
                                    }
                            }

                        let result = dl |> PrescriptionRule.adjustDoseLimitToPatient None pat15kg

                        result.QuantityAdjust
                        |> Expect.notEqual "adjust should be preserved" MinMax.empty
                    }

                    test "pins to min when adjust max * adj <= absolute min" {
                        let dl =
                            { DoseLimit.limit with
                                AdjustUnit = Some kg
                                DoseUnit = mg
                                Quantity =
                                    {
                                        Min = Some(mkLimit 20N mg)
                                        Max = None
                                    }
                                QuantityAdjust =
                                    {
                                        Min = None
                                        Max = Some(mkLimit 1N mgPerKg)
                                    }
                            }

                        let result = dl |> PrescriptionRule.adjustDoseLimitToPatient None pat15kg

                        result.QuantityAdjust |> Expect.equal "adjust should be cleared" MinMax.empty

                        result.Quantity.Max
                        |> Expect.equal "max should be pinned to min" result.Quantity.Min
                    }

                    test "no change when no adjust unit" {
                        let dl =
                            { DoseLimit.limit with
                                DoseUnit = mg
                                Quantity =
                                    {
                                        Min = Some(mkLimit 10N mg)
                                        Max = Some(mkLimit 100N mg)
                                    }
                            }

                        let result = dl |> PrescriptionRule.adjustDoseLimitToPatient None pat15kg

                        result |> Expect.equal "should be unchanged" dl
                    }

                    // Regression tests for the frequency-based reconciliation of an
                    // adjusted per-time target against an absolute quantity cap.
                    // See ceftriaxon 24 kg: 100 mg/kg/day, qty max 2000 mg, freqs {1;2}.

                    // The ceftriaxon 24 kg DoseLimit: adjusted per-time target of
                    // 100 mg/kg/day, absolute per-time max 4000 mg/day and an
                    // absolute quantity cap.
                    let ceftriaxonLike qtyMax =
                        { DoseLimit.limit with
                            AdjustUnit = Some kg
                            DoseUnit = mg
                            Quantity =
                                {
                                    Min = None
                                    Max = Some(mkLimit qtyMax mg)
                                }
                            PerTime =
                                {
                                    Min = None
                                    Max = Some(mkLimit 4000N mgPerDay)
                                }
                            PerTimeAdjust =
                                {
                                    Min = Some(mkLimit 100N mgPerKgPerDay)
                                    Max = Some(mkLimit 100N mgPerKgPerDay)
                                }
                        }

                    test "keeps target and does not pin quantity when reachable at a higher frequency" {
                        // 100 mg/kg/day * 24 kg = 2400 mg/day. Unreachable in one dose
                        // (>2000 cap) but reachable at freq 2 (1200 mg x2 = 2400 mg/day).
                        let dl = ceftriaxonLike 2000N

                        let result =
                            dl
                            |> PrescriptionRule.adjustDoseLimitToPatient (Some(freqSet [ 1I; 2I ])) pat24kg

                        result.Quantity.Min
                        |> Expect.isNone "quantity should not be pinned when target is reachable"

                        result.PerTimeAdjust
                        |> Expect.notEqual "adjusted per-time target should be preserved" MinMax.empty
                    }

                    test "pins quantity to max and clears per-time adjust when unreachable at any frequency" {
                        // qty cap 500 mg: even at freq 2 the needed per-dose is
                        // 2400/2 = 1200 mg > 500, so the absolute cap must win.
                        let dl = ceftriaxonLike 500N

                        let result =
                            dl
                            |> PrescriptionRule.adjustDoseLimitToPatient (Some(freqSet [ 1I; 2I ])) pat24kg

                        result.Quantity.Min
                        |> Expect.equal "quantity min should be pinned to max" result.Quantity.Max

                        result.PerTimeAdjust
                        |> Expect.equal "adjusted per-time target should be cleared" MinMax.empty
                    }

                    test "frequency blocks are skipped when no frequencies are given" {
                        // With no frequency set neither maxFreq nor minFreq exist,
                        // so the freq-based reconciliation must not fire.
                        let dl = ceftriaxonLike 2000N

                        let result = dl |> PrescriptionRule.adjustDoseLimitToPatient None pat24kg

                        result.Quantity.Min |> Expect.isNone "quantity should be untouched"

                        result.PerTimeAdjust
                        |> Expect.notEqual "adjusted per-time target should be preserved" MinMax.empty
                    }
                ]


    /// Data-driven regression over real NKF dose rules that are bounded by an
    /// absolute MaxQty and also carry an adjusted dose limit. Each rule is run,
    /// through the real parse -> getDoseLimits -> adjustDoseLimitToPatient
    /// pipeline, with a patient adjustment sized to conflict with the adjusted
    /// limit. The resolved DoseLimit must always leave at least one feasible
    /// frequency (otherwise the solver fails with an empty frequency set).
    module MaxQtyConflictTests =

        let kg = Units.Weight.kiloGram
        let cm = Units.Height.centiMeter

        // --- build DoseRuleData rows programmatically, by column NAME ---

        let headerCols =
            [|
                "RowId"
                "RuleId"
                "GrpId"
                "SortNo"
                "Source"
                "Generic"
                "Form"
                "Brand"
                "Route"
                "GPKs"
                "HPKs"
                "Indication"
                "SourceText"
                "PatientText"
                "ScheduleText"
                "Loc"
                "Dep"
                "IsAdult"
                "Gender"
                "MinAge"
                "MaxAge"
                "MinWeight"
                "MaxWeight"
                "MinBSA"
                "MaxBSA"
                "MinGestAge"
                "MaxGestAge"
                "MinPMAge"
                "MaxPMAge"
                "DoseType"
                "DoseText"
                "CmpBased"
                "Component"
                "Substance"
                "Freqs"
                "DoseUnit"
                "AdjustUnit"
                "FreqUnit"
                "RateUnit"
                "MinTime"
                "MaxTime"
                "TimeUnit"
                "MinInt"
                "MaxInt"
                "IntUnit"
                "MinDur"
                "MaxDur"
                "DurUnit"
                "MinQty"
                "MaxQty"
                "MinQtyAdj"
                "MaxQtyAdj"
                "MinPerTime"
                "MaxPerTime"
                "MinPerTimeAdj"
                "MaxPerTimeAdj"
                "MinRate"
                "MaxRate"
                "MinRateAdj"
                "MaxRateAdj"
                "Validated"
                "FreqCheck"
                "DoseCheck"
            |]

        let mkRow (fields: (string * string) list) =
            let m = Map.ofList fields
            headerCols |> Array.map (fun h -> m |> Map.tryFind h |> Option.defaultValue "")

        let baseFields gen route dose freqs du au fu =
            [
                "Source", "NKF"
                "Generic", gen
                "Route", route
                "DoseType", dose
                "Component", gen
                "Substance", gen
                "Freqs", freqs
                "DoseUnit", du
                "AdjustUnit", au
                "FreqUnit", fu
            ]

        // Only MaxQty-bounded rules with an adjusted dose limit.
        // Note: rules whose adjusted target only conflicts at a physically
        // unreachable body size are intentionally excluded — e.g. aciclovir
        // (1000 mg/m2/day, max 2000 mg/dose, 3x/day) would need a BSA of ~6 m2
        // to conflict, so it cannot induce a realistic adjusted-vs-MaxQty
        // conflict and is not a meaningful test case.
        let cases: (string * (string * string) list) list =
            [
                // PerTimeAdjust vs MaxQty (exercises the fixed freq block)
                "bupropion",
                baseFields "bupropion" "ORAAL" "discontinuous" "1;2" "mg" "kg" "dag"
                @ [
                    "MaxQty", "150"
                    "MaxPerTime", "300"
                    "MinPerTimeAdj", "3"
                ]
                "carbamazepine",
                baseFields "carbamazepine" "ORAAL" "discontinuous" "2;3" "mg" "kg" "dag"
                @ [
                    "MaxQty", "1000"
                    "MaxPerTime", "1000"
                    "MinPerTimeAdj", "10"
                    "MaxPerTimeAdj", "10"
                ]
                "ceftaroline<12",
                baseFields "ceftaroline" "INTRAVENEUS" "timed" "3" "mg" "kg" "dag"
                @ [
                    "MaxQty", "400"
                    "MinPerTimeAdj", "36"
                    "MaxPerTimeAdj", "36"
                ]
                "ceftaroline>12",
                baseFields "ceftaroline" "INTRAVENEUS" "timed" "3" "mg" "kg" "dag"
                @ [
                    "MaxQty", "400"
                    "MaxPerTime", "1800"
                    "MinPerTimeAdj", "36"
                    "MaxPerTimeAdj", "36"
                ]
                "dasatinib",
                baseFields "dasatinib" "ORAAL" "discontinuous" "1" "mg" "m2" "dag"
                @ [
                    "MaxQty", "110"
                    "MinPerTimeAdj", "65"
                    "MaxPerTimeAdj", "65"
                ]
                "fluconazol",
                baseFields "fluconazol" "ORAAL" "discontinuous" "1" "mg" "kg" "dag"
                @ [
                    "MaxQty", "800"
                    "MaxPerTime", "800"
                    "MinPerTimeAdj", "25"
                    "MaxPerTimeAdj", "25"
                ]
                "fytomenadion",
                baseFields "fytomenadion" "INTRAVENEUS" "discontinuous" "1" "mL" "kg" "dag"
                @ [
                    "MaxQty", "10"
                    "MaxPerTime", "10"
                    "MinPerTimeAdj", "1"
                    "MaxPerTimeAdj", "1"
                ]
                "methylprednisolon",
                baseFields "methylprednisolon" "INTRAVENEUS" "timed" "1" "mg" "kg" "dag"
                @ [
                    "MaxQty", "1000"
                    "MinPerTimeAdj", "10"
                    "MaxPerTimeAdj", "10"
                ]
                "natriumfosfaat",
                baseFields "natriumfosfaat" "RECTAAL" "discontinuous" "1" "mL" "kg" "dag"
                @ [
                    "MaxQty", "133"
                    "MinPerTimeAdj", "2.5"
                    "MaxPerTimeAdj", "2.5"
                ]
                "posaconazol",
                baseFields "posaconazol" "INTRAVENEUS" "discontinuous" "2" "mg" "kg" "dag"
                @ [
                    "MaxQty", "300"
                    "MinPerTimeAdj", "12"
                    "MaxPerTimeAdj", "12"
                ]
                "rifampicine",
                baseFields "rifampicine" "ORAAL" "discontinuous" "1" "mg" "kg" "dag"
                @ [
                    "MaxQty", "600"
                    "MinPerTimeAdj", "20"
                    "MaxPerTimeAdj", "20"
                ]

                // QuantityAdjust vs MaxQty (regression: already-correct block)
                "adenosine",
                baseFields "adenosine" "INTRAVENEUS" "once" "" "microg" "kg" ""
                @ [ "MaxQty", "6000"; "MinQtyAdj", "100"; "MaxQtyAdj", "100" ]
                "albendazol",
                baseFields "albendazol" "ORAAL" "once" "" "mg" "kg" ""
                @ [ "MaxQty", "400"; "MinQtyAdj", "15"; "MaxQtyAdj", "15" ]
                "alimemazine",
                baseFields "alimemazine" "ORAAL" "discontinuous" "1" "mg" "kg" "dag"
                @ [ "MaxQty", "50"; "MinQtyAdj", "2"; "MaxQtyAdj", "4" ]
                "amiodaron-1",
                baseFields "amiodaron" "INTRAVENEUS" "onceTimed" "" "mg" "kg" ""
                @ [ "MaxQty", "300"; "MinQtyAdj", "5"; "MaxQtyAdj", "5" ]
                "amiodaron-2",
                baseFields "amiodaron" "INTRAVENEUS" "onceTimed" "" "mg" "kg" ""
                @ [ "MaxQty", "150"; "MinQtyAdj", "5"; "MaxQtyAdj", "5" ]
                "diclofenac",
                baseFields "diclofenac" "INTRAVENEUS" "discontinuous" "1;2;3;4" "mg" "kg" "dag"
                @ [ "MaxQty", "37.5"; "MinQtyAdj", "0.3"; "MaxQtyAdj", "0.5" ]
                "fysostigmine",
                baseFields "fysostigmine" "INTRAVENEUS" "discontinuous" "1;2;3;4" "mg" "kg" "dag"
                @ [
                    "MaxQty", "0.5"
                    "MinQtyAdj", "0.02"
                    "MaxQtyAdj", "0.02"
                    "MaxPerTime", "2"
                ]
                "mepivacaine",
                baseFields "mepivacaine" "EPIDURAAL" "once" "" "mg" "kg" ""
                @ [ "MaxQty", "400"; "MinQtyAdj", "10" ]
                "metamizol",
                baseFields "metamizol" "INTRAVENEUS" "discontinuous" "1;2;3;4" "mg" "kg" "dag"
                @ [
                    "MaxQty", "1000"
                    "MinQtyAdj", "8"
                    "MaxQtyAdj", "16"
                    "MaxPerTime", "4000"
                ]
                "midazolam",
                baseFields "midazolam" "OROMUCOSAAL" "once" "" "mg" "kg" ""
                @ [ "MaxQty", "10"; "MinQtyAdj", "0.2"; "MaxQtyAdj", "0.5" ]
                "pembrolizumab",
                baseFields "pembrolizumab" "INTRAVENEUS" "timed" "1" "mg" "kg" "week"
                @ [ "MaxQty", "200"; "MinQtyAdj", "2"; "MaxQtyAdj", "2" ]
                "tocilizumab",
                baseFields "tocilizumab" "INTRAVENEUS" "once" "" "mg" "kg" ""
                @ [ "MaxQty", "800"; "MinQtyAdj", "12"; "MaxQtyAdj", "12" ]
                "tramadol",
                baseFields "tramadol" "ORAAL" "discontinuous" "1;2;3;4" "mg" "kg" "dag"
                @ [
                    "MaxQty", "100"
                    "MinQtyAdj", "1"
                    "MaxQtyAdj", "2"
                    "MaxPerTime", "400"
                    "MaxPerTimeAdj", "8"
                ]
                "ibuprofen",
                baseFields "ibuprofen" "INTRAVENEUS" "discontinuous" "1;2;3;4" "mg" "kg" "dag"
                @ [
                    "MaxQty", "400"
                    "MinQtyAdj", "10"
                    "MaxQtyAdj", "10"
                    "MaxPerTimeAdj", "40"
                ]
            ]

        let data =
            cases
            |> List.map (fun (label, fs) -> mkRow (("RowId", label) :: fs))
            |> List.toArray
            |> Array.append [| headerCols |]

        let parseResult = DoseRuleData.parseDoseRuleData data

        let parsed =
            match parseResult with
            | Ok ds -> ds
            | Error _ -> [||]

        let doseLimits = parsed |> DoseRule.getDoseLimits

        // --- helpers (mirror the exploratory MaxQtyConflicts.fsx script) ---

        let scale (r: BigRational) vu =
            vu |> ValueUnit.applyToValue (Array.map (fun x -> x * r))

        /// The adjust value (kg or m2) that just pushes the adjusted dose past
        /// MaxQty at the lowest frequency. For multi-frequency rules this lands
        /// in the "reachable at a higher frequency" window.
        let conflictingAdj (dl: DoseLimit) (minFreq: ValueUnit option) =
            match dl.AdjustUnit, dl.Quantity.Max |> Option.map Limit.getValueUnit with
            | Some _, Some qmax ->
                match dl.QuantityAdjust.Min |> Option.map Limit.getValueUnit with
                | Some qadjMin -> qmax / qadjMin |> scale (11N / 10N) |> Some
                | None ->
                    match dl.PerTimeAdjust.Min |> Option.map Limit.getValueUnit, minFreq with
                    | Some ptmMin, Some f -> qmax * f / ptmMin |> scale (11N / 10N) |> Some
                    | _ -> None
            | _ -> None

        let bsaOf w h =
            { Patient.patient with
                Weight = Some w
                Height = Some h
            }
            |> Patient.calcBSA

        /// A patient whose weight (kg) or BSA (m2) equals the target adjust value.
        let patientFor (dl: DoseLimit) (adjVU: ValueUnit) =
            if dl.AdjustUnit.Value |> Units.eqsUnit kg then
                { Patient.patient with
                    Weight = Some(adjVU |> ValueUnit.convertTo kg)
                    Height = Some(ValueUnit.singleWithUnit cm 150N)
                }
            else
                let h = ValueUnit.singleWithUnit cm 175N
                let mutable w = 1
                let mutable found = None

                while found.IsNone && w <= 400 do
                    let wv = ValueUnit.singleWithUnit kg (BigRational.fromInt w)

                    match bsaOf wv h with
                    | Some b when (b >? adjVU) || b = adjVU -> found <- Some wv
                    | _ -> ()

                    w <- w + 1

                match found with
                | Some wv ->
                    { Patient.patient with
                        Weight = Some wv
                        Height = Some h
                    }
                | None ->
                    // fail loudly rather than fall back to a size that does not
                    // actually conflict, which would make the test vacuous
                    failwithf
                        "patientFor: BSA %s is unreachable within 400 kg; this rule cannot induce a realistic adjusted-vs-MaxQty conflict"
                        (adjVU |> ValueUnit.toStringDecimalDutchShortWithPrec 2)

        let freqOf (d: DoseRuleData) =
            if d.ScheduleData.Freqs |> Array.isEmpty then
                None
            else
                d.ScheduleData.FreqUnit
                |> Utils.Units.freqUnit
                |> Option.map (fun fu -> d.ScheduleData.Freqs |> ValueUnit.withUnit fu)

        /// Model of the solver step: is there a frequency in the rule's set for
        /// which a valid quantity satisfies the surviving PerTimeAdjust bounds and
        /// the absolute PerTime cap? Returns false when no frequency works (the
        /// reported empty-frequency-set crash).
        ///
        /// - Pinned quantity: each frequency gives one adjusted per-time value that
        ///   must sit within both PerTimeAdjust bounds.
        /// - Unpinned quantity: the dose is free in (0, qmax], so upper bounds are
        ///   always satisfiable by a smaller dose; feasibility hinges on reaching
        ///   the lower target at the maximum quantity, and on the absolute PerTime
        ///   cap admitting that adjusted target.
        let isSolvable (dl: DoseLimit) (pat: Patient) (freqVU: ValueUnit option) =
            match dl.AdjustUnit, freqVU, dl.Quantity.Max |> Option.map Limit.getValueUnit with
            | Some au, Some fvu, Some qmax ->
                let adj =
                    (if au |> Units.eqsUnit kg then
                         pat.Weight
                     else
                         pat |> Patient.calcBSA)
                    |> Option.get

                let pinned =
                    match dl.Quantity.Min |> Option.map Limit.getValueUnit with
                    | Some qmin -> qmin = qmax
                    | None -> false

                let fu = fvu |> ValueUnit.getUnit

                let ge v l =
                    v >? Limit.getValueUnit l || v = Limit.getValueUnit l

                let le v l =
                    v <? Limit.getValueUnit l || v = Limit.getValueUnit l

                // the absolute PerTime cap must be able to admit the lower adjusted
                // target (an unpinned quantity can otherwise not both reach the
                // adjusted minimum and stay under the absolute maximum)
                let capAdmitsTarget =
                    match dl.PerTimeAdjust.Min, dl.PerTime.Max with
                    | Some pm, Some cap -> le (Limit.getValueUnit pm * adj) cap
                    | _ -> true

                capAdmitsTarget
                && (fvu
                    |> ValueUnit.getValue
                    |> Array.exists (fun fv ->
                        // adjusted per-time at the maximum allowed quantity
                        let ptmAtMax = (qmax / adj) * ValueUnit.singleWithUnit fu fv
                        let geMin = dl.PerTimeAdjust.Min |> Option.forall (ge ptmAtMax)
                        // pinned: the single value must also respect the upper bound;
                        // unpinned: a smaller dose always can, so no upper check
                        let leMax =
                            if pinned then
                                dl.PerTimeAdjust.Max |> Option.forall (le ptmAtMax)
                            else
                                true

                        geMin && leMax
                    ))
            | _ -> true

        /// Resolve one rule with a conflict-inducing patient. None when no
        /// adjusted-vs-MaxQty conflict is possible for the rule.
        let resolveConflict (d: DoseRuleData) (dl: DoseLimit) =
            let freqVU = freqOf d
            let minFreq = freqVU |> Option.bind ValueUnit.minValue

            conflictingAdj dl minFreq
            |> Option.map (fun adjVU ->
                let pat = patientFor dl adjVU
                dl |> PrescriptionRule.adjustDoseLimitToPatient freqVU pat, pat, freqVU
            )

        let byLabel =
            Array.zip parsed doseLimits
            |> Array.map (fun (d, dl) -> d.RowId, (d, dl))
            |> Map.ofArray

        let tests =
            testList
                "MaxQty adjusted-dose conflicts"
                [
                    test "fixture parses and maps one dose limit per rule" {
                        parseResult |> Result.isOk |> Expect.isTrue "fixture should parse"

                        doseLimits.Length |> Expect.equal "one dose limit per rule" cases.Length
                    }

                    // one test per MaxQty-bounded rule: the resolved DoseLimit must
                    // leave at least one feasible frequency (no empty-set crash)
                    for d, dl in Array.zip parsed doseLimits do
                        test $"{d.RowId}: adjusted-vs-MaxQty conflict resolves to a solvable dose limit" {
                            match resolveConflict d dl with
                            | None -> failtestf "%s: expected an adjusted-vs-MaxQty conflict to test" d.RowId
                            | Some(res, pat, freqVU) ->
                                isSolvable res pat freqVU
                                |> Expect.isTrue $"{d.RowId} should have a feasible frequency after adjustment"
                        }

                    // explicit branch coverage
                    test "multi-frequency rule (bupropion) preserves the adjusted target" {
                        let d, dl = byLabel |> Map.find "bupropion"

                        match resolveConflict d dl with
                        | None -> failtest "bupropion: expected a conflict"
                        | Some(res, _, _) ->
                            res.Quantity.Min |> Expect.isNone "quantity should not be pinned"

                            res.PerTimeAdjust
                            |> Expect.notEqual "adjusted target should be preserved" MinMax.empty
                    }

                    test "single-frequency rule (rifampicine) pins to max and clears the adjusted target" {
                        let d, dl = byLabel |> Map.find "rifampicine"

                        match resolveConflict d dl with
                        | None -> failtest "rifampicine: expected a conflict"
                        | Some(res, _, _) ->
                            res.Quantity.Min |> Expect.equal "quantity min pinned to max" res.Quantity.Max

                            res.PerTimeAdjust |> Expect.equal "adjusted target cleared" MinMax.empty
                    }
                ]


    module PatientCategoryTests =


        let tests =
            testList
                "PatientCategory"
                [
                    let filter = Filter.doseFilter

                    let patCat =
                        {
                            Location = None
                            Department = None
                            Gender = AnyGender
                            Age = MinMax.empty |> AbsoluteAge
                            Weight = MinMax.empty
                            BSA = MinMax.empty
                            GestAge = MinMax.empty
                            PMAge = MinMax.empty
                            Access = AnyAccess
                        }

                    test "an empty filter and empty patient category" {
                        patCat |> PatientCategory.filter filter |> Expect.isTrue "should return true"
                    }

                    test "an empty filter and patient category with female gender" {
                        { patCat with Gender = Female }
                        |> PatientCategory.filter filter
                        |> Expect.isFalse "should return false"
                    }

                    test "a filter with female gender and patient category with female gender" {
                        { patCat with Gender = Female }
                        |> PatientCategory.filter { filter with DoseFilter.Patient.Gender = Female }
                        |> Expect.isTrue "should return true"
                    }

                    test "a filter with female gender and patient category with no gender" {
                        { patCat with Gender = AnyGender }
                        |> PatientCategory.filter { filter with DoseFilter.Patient.Gender = Female }
                        |> Expect.isTrue "should return true"
                    }

                    test "an empty filter and a patient category with a max age of 7" {
                        { patCat with
                            Age =
                                { MinMax.empty with
                                    Max = 7N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter filter
                        |> Expect.isFalse "should return false"
                    }

                    test "a filter with age 5 and a patient category with a max age of 7" {
                        { patCat with
                            Age =
                                { MinMax.empty with
                                    Max = 7N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                DoseFilter.Patient.Age = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                            }
                        |> Expect.isTrue "should return true"
                    }

                    test "a filter with age 5 and a patient category with a min age of 1 week" {
                        { patCat with
                            Age =
                                { MinMax.empty with
                                    Min = 1N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                DoseFilter.Patient.Age = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                            }
                        |> Expect.isFalse "should return false"
                    }

                    test "a filter with age 5 and a patient category with a min age of 3 and max age of 7" {
                        { patCat with
                            Age =
                                {
                                    Min = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 7N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                DoseFilter.Patient.Age = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                            }
                        |> Expect.isTrue "should return true"
                    }

                    test
                        "a filter with age 5 with a patient category with a min age of 3 and max age of 7 and gender female" {
                        { patCat with
                            Gender = Female
                            Age =
                                {
                                    Min = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 7N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                DoseFilter.Patient.Age = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                            }
                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 5, gender female with a patient category with a min age of 3 and max age of 7 and gender female" {
                        { patCat with
                            Gender = Female
                            Age =
                                {
                                    Min = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 7N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Gender = Female
                                        Age = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                    }
                            }
                        |> Expect.isTrue "should return true"
                    }

                    test "a filter with age 0 and gestational age 30 weeks with an empty patient category" {
                        patCat
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 30N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                            }
                        |> Expect.isTrue "should return true"
                    }

                    test "a filter with age 0 and gestational age 30 weeks with a patient category with min age = 0" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = None
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 30N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                            }
                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 0 and gestational age 28 weeks, weight = 1.15 kg, height = 46 cm, with a patient category with min age = 0" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = None
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                        Weight = (115N / 100N) |> ValueUnit.singleWithUnit Units.Weight.kiloGram |> Some
                                        Height = 45N |> ValueUnit.singleWithUnit Units.Height.centiMeter |> Some
                                    }
                            }
                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 0 and gestational age 30 weeks with a patient category with a min age of 0 and max age of 28 and gestational age min 28 and max 32 weeks" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            GestAge =
                                {
                                    Min = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 30N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                            }
                        |> Expect.isTrue "should return true"
                    }

                    test
                        "a filter with age 0 and gestational age 37 weeks with a patient category with a min age of 0 and max age of 28 and gestational age min 28 and max 32 weeks" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            GestAge =
                                {
                                    Min = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 33N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                            }
                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 8 and gestational age 27 weeks with a patient category with a min age of 0 and max age of 28 and gestational age min 28 and max 32 weeks" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            GestAge =
                                {
                                    Min = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 8N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 27N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                                    |> Patient.calcPMAge
                            }
                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 0 and gestational age 30 weeks with a patient category with a min age of 0 and max age of 28 and pm age min 28 and max 32 weeks" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            PMAge =
                                {
                                    Min = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 30N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                                    |> Patient.calcPMAge
                            }

                        |> Expect.isTrue "should return true"
                    }

                    test
                        "a filter with age 0 and gestational age 37 weeks with a patient category with a min age of 0 and max age of 28 and pm age min 28 and max 32 weeks" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            PMAge =
                                {
                                    Min = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 37N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                                    |> Patient.calcPMAge
                            }

                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 8 and gestational age 27 weeks with a patient category with a min age of 0 and max age of 28 and pm age min 28 and max 32 weeks" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            PMAge =
                                {
                                    Min = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 8N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 27N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                                    |> Patient.calcPMAge
                            }
                        |> Expect.isTrue "should return true"
                    }

                    test
                        "a filter with age 0, ga = 32 and weight 1.45 with a patient category with max age = 30 and max gest 37 and max weight 1.5" {
                        { patCat with
                            Age =
                                { MinMax.empty with
                                    Max = 30N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            Weight =
                                { patCat.Weight with
                                    Max =
                                        1.5m
                                        |> BigRational.FromDecimal
                                        |> ValueUnit.singleWithUnit Units.Weight.kiloGram
                                        |> Limit.Inclusive
                                        |> Some
                                }
                            GestAge.Max = 37N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        Weight =
                                            1.45m
                                            |> BigRational.FromDecimal
                                            |> ValueUnit.singleWithUnit Units.Weight.kiloGram
                                            |> Some
                                        GestAge = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                            }

                        |> Expect.isTrue "should return true"
                    }

                    test
                        "a filter with age 0, ga = 32 and weight 1.45 with a patient category with min age = 30 and max age = 720" {
                        { patCat with
                            Age =
                                {
                                    Min = 30N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 720N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        Weight = (145N / 10N) |> ValueUnit.singleWithUnit Units.Weight.kiloGram |> Some
                                        GestAge = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                            }

                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 0, ga = None and weight 3500 gram with a patient category with pm age = 36 and max age = 37" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            PMAge =
                                {
                                    Min = 36N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 37N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Exclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        Weight = 3500N |> ValueUnit.singleWithUnit Units.Weight.gram |> Some
                                    }
                            }

                        |> Expect.isFalse "should return false"
                    }

                    test "an empty patient category is a match with another empty patient category" {
                        PatientCategory.empty
                        |> PatientCategory.isMatch PatientCategory.empty
                        |> Expect.isTrue "should return true"
                    }

                    testList
                        "patient category tests"
                        [

                            fun minAge maxAge ->
                                let minAge = if minAge < 0N then None else Some minAge
                                let maxAge = if maxAge < 0N then None else Some maxAge
                                let minAge, maxAge = if minAge > maxAge then maxAge, minAge else minAge, maxAge

                                let patCatToMatch =
                                    { PatientCategory.empty with
                                        Age =
                                            {
                                                Min =
                                                    minAge
                                                    |> Option.map (
                                                        ValueUnit.singleWithUnit Units.Time.day >> Limit.Inclusive
                                                    )
                                                Max =
                                                    maxAge
                                                    |> Option.map (
                                                        ValueUnit.singleWithUnit Units.Time.day >> Limit.Inclusive
                                                    )
                                            }
                                            |> AbsoluteAge
                                    }

                                let emptyPatCat = PatientCategory.empty
                                emptyPatCat |> PatientCategory.isMatch patCatToMatch
                            |> Generators.testProp
                                "a patient cat with age should always match an empty patient category"

                            fun minWeight maxWeight ->
                                let minWeight = if minWeight < 0N then None else Some minWeight
                                let maxWeight = if maxWeight < 0N then None else Some maxWeight

                                let minWeight, maxWeight =
                                    if minWeight > maxWeight then
                                        maxWeight, minWeight
                                    else
                                        minWeight, maxWeight

                                let patCatToMatch =
                                    { PatientCategory.empty with
                                        Weight =
                                            {
                                                Min =
                                                    minWeight
                                                    |> Option.map (
                                                        ValueUnit.singleWithUnit Units.Weight.kiloGram
                                                        >> Limit.Inclusive
                                                    )
                                                Max =
                                                    maxWeight
                                                    |> Option.map (
                                                        ValueUnit.singleWithUnit Units.Weight.kiloGram
                                                        >> Limit.Inclusive
                                                    )
                                            }
                                    }

                                let emptyPatCat = PatientCategory.empty
                                emptyPatCat |> PatientCategory.isMatch patCatToMatch
                            |> Generators.testProp
                                "a patient cat with weight should always match an empty patient category"

                            fun gender ->
                                let patCatToMatch = { PatientCategory.empty with Gender = gender }
                                let emptyPatCat = PatientCategory.empty
                                emptyPatCat |> PatientCategory.isMatch patCatToMatch
                            |> Generators.testProp
                                "a patient cat with a gender should always match an empty patient category"

                            fun location ->
                                let patCatToMatch = { PatientCategory.empty with Access = location }
                                let emptyPatCat = PatientCategory.empty
                                emptyPatCat |> PatientCategory.isMatch patCatToMatch
                            |> Generators.testProp
                                "a patient cat with a location should always match an empty patient category"

                            fun minAge maxAge ->
                                let minAge = if minAge < 0N then Some 0N else Some minAge
                                let maxAge = if maxAge < 0N then Some 0N else Some maxAge
                                let minAge, maxAge = if minAge > maxAge then maxAge, minAge else minAge, maxAge

                                let notEmptyPatCat =
                                    { PatientCategory.empty with
                                        Age =
                                            {
                                                Min =
                                                    minAge
                                                    |> Option.map (
                                                        ValueUnit.singleWithUnit Units.Time.day >> Limit.Inclusive
                                                    )
                                                Max =
                                                    maxAge
                                                    |> Option.map (
                                                        ValueUnit.singleWithUnit Units.Time.day >> Limit.Inclusive
                                                    )
                                            }
                                            |> AbsoluteAge
                                    }

                                let patCatToWatch = PatientCategory.empty
                                notEmptyPatCat |> PatientCategory.isMatch patCatToWatch |> not
                            |> Generators.testProp "an empty pat cat should never match an patient category with age"

                        ]
                ]


    module DoseTypeTests =


        let tests =
            testList
                "DoseType"
                [

                    test "sortBy Once = 0" { DoseType.sortBy (Once "") |> Expect.equal "Once should sort first" 0 }

                    test "sortBy OnceTimed = 0" {
                        DoseType.sortBy (OnceTimed "") |> Expect.equal "OnceTimed should sort first" 0
                    }

                    test "sortBy Discontinuous = 3" {
                        DoseType.sortBy (Discontinuous "")
                        |> Expect.equal "Discontinuous should sort at 3" 3
                    }

                    test "sortBy Timed = 3" { DoseType.sortBy (Timed "") |> Expect.equal "Timed should sort at 3" 3 }

                    test "sortBy Continuous = 4" {
                        DoseType.sortBy (Continuous "") |> Expect.equal "Continuous should sort at 4" 4
                    }

                    test "sortBy NoDoseType = 100" {
                        DoseType.sortBy NoDoseType |> Expect.equal "NoDoseType should sort last" 100
                    }

                    test "fromString 'once' produces Once constructor" {
                        let dt = DoseType.fromString "once" "eenmalig"

                        match dt with
                        | Once _ -> ()
                        | other -> failtest $"expected Once, got %A{other}"
                    }

                    test "fromString 'continuous' produces Continuous constructor" {
                        let dt = DoseType.fromString "continuous" "continu"

                        match dt with
                        | Continuous _ -> ()
                        | other -> failtest $"expected Continuous, got %A{other}"
                    }

                    test "fromString unknown input produces NoDoseType" {
                        DoseType.fromString "unknown" ""
                        |> Expect.equal "unknown type should give NoDoseType" NoDoseType
                    }

                    test "toString returns type for Once with empty text" {
                        DoseType.toString (Once "") |> Expect.equal "should be 'once'" "once"
                    }

                    test "toString returns type and text for Timed with text" {
                        DoseType.toString (Timed "onderhoud")
                        |> Expect.equal "should be 'timed onderhoud'" "timed onderhoud"
                    }

                    test "getText returns payload for Discontinuous" {
                        DoseType.getText (Discontinuous "onderhoud")
                        |> Expect.equal "should return 'onderhoud'" "onderhoud"
                    }

                    test "getText returns empty string for NoDoseType" {
                        DoseType.getText NoDoseType |> Expect.equal "NoDoseType → empty" ""
                    }

                    test "toDescription returns Dutch fallback for Once with empty text" {
                        DoseType.toDescription (Once "")
                        |> Expect.equal "Once fallback should be 'eenmalig'" "eenmalig"
                    }

                    test "toDescription returns Dutch fallback for Continuous with empty text" {
                        DoseType.toDescription (Continuous "")
                        |> Expect.equal "Continuous fallback should be 'continu'" "continu"
                    }

                    test "toDescription returns text when set" {
                        DoseType.toDescription (Once "special")
                        |> Expect.equal "explicit text should be returned" "special"
                    }

                    test "eqs is true for same constructor same text case-insensitive" {
                        DoseType.eqs (Once "A") (Once "a")
                        |> Expect.isTrue "case-insensitive eqs should match"
                    }

                    test "eqs is false for different constructors" {
                        DoseType.eqs (Once "a") (Continuous "a")
                        |> Expect.isFalse "different constructors should not be equal"
                    }

                    test "eqsType is true for same constructor different text" {
                        DoseType.eqsType (Once "A") (Once "B")
                        |> Expect.isTrue "same type regardless of text"
                    }

                    test "eqsType is false for different constructors" {
                        DoseType.eqsType (Once "") (Timed "")
                        |> Expect.isFalse "different constructors → false"
                    }

                    test "setDescription replaces text payload" {
                        DoseType.setDescription "new" (Once "old")
                        |> DoseType.getText
                        |> Expect.equal "payload should be replaced" "new"
                    }

                    test "setDescription preserves constructor" {
                        let dt = DoseType.setDescription "x" (Discontinuous "old")

                        match dt with
                        | Discontinuous _ -> ()
                        | other -> failtest $"constructor changed: %A{other}"
                    }
                ]


    module LimitTargetTests =


        let tests =
            testList
                "LimitTarget"
                [

                    test "toString NoLimitTarget returns empty string" {
                        LimitTarget.toString NoLimitTarget |> Expect.equal "should be empty" ""
                    }

                    test "toString OrderableLimitTarget returns empty string" {
                        LimitTarget.toString OrderableLimitTarget |> Expect.equal "should be empty" ""
                    }

                    test "toString SubstanceLimitTarget returns label" {
                        LimitTarget.toString (SubstanceLimitTarget "paracetamol")
                        |> Expect.equal "should return label" "paracetamol"
                    }

                    test "toString ComponentLimitTarget returns label" {
                        LimitTarget.toString (ComponentLimitTarget "comp1")
                        |> Expect.equal "should return label" "comp1"
                    }

                    test "isOrderableTarget true only for OrderableLimitTarget" {
                        LimitTarget.isOrderableTarget OrderableLimitTarget
                        |> Expect.isTrue "OrderableLimitTarget should match"
                    }

                    test "isOrderableTarget false for SubstanceLimitTarget" {
                        LimitTarget.isOrderableTarget (SubstanceLimitTarget "x")
                        |> Expect.isFalse "SubstanceLimitTarget should not match"
                    }

                    test "isComponentTarget true for ComponentLimitTarget" {
                        LimitTarget.isComponentTarget (ComponentLimitTarget "c")
                        |> Expect.isTrue "ComponentLimitTarget should match"
                    }

                    test "isSubstanceTarget true for SubstanceLimitTarget" {
                        LimitTarget.isSubstanceTarget (SubstanceLimitTarget "s")
                        |> Expect.isTrue "SubstanceLimitTarget should match"
                    }

                    test "isSubstanceTarget false for ComponentLimitTarget" {
                        LimitTarget.isSubstanceTarget (ComponentLimitTarget "c")
                        |> Expect.isFalse "ComponentLimitTarget is not substance target"
                    }

                    test "componentTargetToString returns label for ComponentLimitTarget" {
                        LimitTarget.componentTargetToString (ComponentLimitTarget "comp")
                        |> Expect.equal "should return label" "comp"
                    }

                    test "componentTargetToString returns empty for SubstanceLimitTarget" {
                        LimitTarget.componentTargetToString (SubstanceLimitTarget "s")
                        |> Expect.equal "non-component → empty" ""
                    }

                    test "substanceTargetToString returns label for SubstanceLimitTarget" {
                        LimitTarget.substanceTargetToString (SubstanceLimitTarget "sub")
                        |> Expect.equal "should return label" "sub"
                    }

                    test "substanceTargetToString returns empty for ComponentLimitTarget" {
                        LimitTarget.substanceTargetToString (ComponentLimitTarget "c")
                        |> Expect.equal "non-substance → empty" ""
                    }

                    test "DoseLimit.isSubstanceLimit true when SubstanceLimitTarget is set" {
                        let dl =
                            { DoseLimit.limit with DoseLimitTarget = SubstanceLimitTarget "paracetamol" }

                        dl
                        |> DoseLimit.isSubstanceLimit
                        |> Expect.isTrue "should detect substance limit"
                    }

                    test "DoseLimit.isComponentLimit true when ComponentLimitTarget is set" {
                        let dl = { DoseLimit.limit with DoseLimitTarget = ComponentLimitTarget "comp" }

                        dl
                        |> DoseLimit.isComponentLimit
                        |> Expect.isTrue "should detect component limit"
                    }

                    test "DoseLimit.isShapeLimit true when OrderableLimitTarget is set" {
                        let dl = { DoseLimit.limit with DoseLimitTarget = OrderableLimitTarget }

                        dl
                        |> DoseLimit.isShapeLimit
                        |> Expect.isTrue "should detect shape/orderable limit"
                    }
                ]


    /// IR Doseringscontrole V-5-0-1 conformance fixes (see code review
    /// docs/code-reviews/genform-check-vs-ir-doseringscontrole-v5-0-1.md).
    module CheckTests =

        open Informedica.Utils.Lib.BCL

        module ZFDR = Informedica.ZForm.Lib.DoseRule

        let private maxVal (mm: MinMax) =
            mm.Max
            |> Option.map (Limit.getValueUnit >> ValueUnit.getValue >> Array.map BigRational.toDouble)

        let private minVal (mm: MinMax) =
            mm.Min
            |> Option.map (Limit.getValueUnit >> ValueUnit.getValue >> Array.map BigRational.toDouble)

        let private mk v u =
            v |> ValueUnit.singleWithUnit u |> Limit.inclusive

        let private mmOf vmin vmax u =
            {
                Min = Some(mk vmin u)
                Max = Some(mk vmax u)
            }

        let private adjUnitStr (mm: MinMax) =
            match mm.Min |> Option.map Limit.getValueUnit with
            | Some vu -> vu |> ValueUnit.getUnit |> Check.unitToString
            | None -> "<empty>"

        let private convKg = Check.setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) None
        let private convM2 = Check.setAdjustAndOrTimeUnit (Some Units.BSA.m2) None

        let private mkMgMax (v: BigRational) =
            { MinMax.empty with Max = v |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Limit.inclusive |> Some }

        let private mkDosage normMax absMax : Informedica.ZForm.Lib.Types.Dosage =
            { ZFDR.Dosage.empty with
                SingleDosage =
                    { ZFDR.DoseRange.empty with
                        Norm = mkMgMax normMax
                        Abs = mkMgMax absMax
                    }
            }

        let private mkRiskDosage highRisk : Informedica.ZForm.Lib.Types.Dosage =
            { mkDosage 1N 1N with HighRisk = highRisk }

        // DI seam: one real DoseRule from the OFFLINE fixtures, so the injected-
        // provider test can exercise checkDoseRuleWithProvider without GStand IO.
        let private fixture<'T> name =
            System.IO.Path.Combine(System.AppContext.BaseDirectory, "fixtures", name)
            |> System.IO.File.ReadAllText
            |> FixtureJson.deSerialize<'T>

        // `lazy` for the same reason as DoseRuleRoundtripTests' fixtures: as a plain
        // value this reads three files and runs the loader in this module's static
        // constructor, during test discovery, where a throw costs the whole assembly
        // its results rather than failing the one test that needs this (issue #523).
        let private sampleDoseRule =
            lazy
                (let data = fixture<DoseRuleData[]> "doserules.json"
                 let rm = fixture<RouteMapping[]> "routemappings.json"
                 let prods = fixture<ProductComponent[]> "products.json"
                 DoseRuleLoader.fromData rm [||] prods data |> fst |> Array.head)

        let tests =
            testList
                "Check IR-doseringscontrole fixes"
                [
                    test "MEDIUM-1 pickAdjust prefers BSA when both present" {
                        Check.pickAdjust
                            (mmOf 5N 10N Units.Mass.milliGram)
                            (mmOf 50N 100N Units.Mass.milliGram)
                            convKg
                            convM2
                        |> adjUnitStr
                        |> Expect.equal "BSA wins" "mg/m2"
                    }

                    test "MEDIUM-1 pickAdjust weight only falls back to kg" {
                        Check.pickAdjust (mmOf 5N 10N Units.Mass.milliGram) MinMax.empty convKg convM2
                        |> adjUnitStr
                        |> Expect.equal "kg" "mg/kg"
                    }

                    test "MEDIUM-1 pickAdjust neither yields empty" {
                        Check.pickAdjust MinMax.empty MinMax.empty convKg convM2
                        |> Expect.equal "empty" MinMax.empty
                    }

                    test "MEDIUM-2 classify within/advisory/absolute/under" {
                        Check.classify 5N (Some 10N) (Some 20N) (Some 2N)
                        |> Expect.equal "within" Check.Within

                        Check.classify 15N (Some 10N) (Some 20N) (Some 2N)
                        |> Expect.equal "advisory" Check.AdvisoryOverNorm

                        Check.classify 25N (Some 10N) (Some 20N) (Some 2N)
                        |> Expect.equal "absolute" Check.OverAbsolute

                        Check.classify 1N (Some 10N) (Some 20N) (Some 2N)
                        |> Expect.equal "under" Check.UnderNorm
                    }

                    test "MEDIUM-2 classify catches absolute breach when normMax is None" {
                        // Hard ceiling must dominate even without a norm max.
                        Check.classify 25N None (Some 20N) None
                        |> Expect.equal "over absolute" Check.OverAbsolute
                    }

                    test "HIGH-1 marginedTestRange is risk-aware and one-sided" {
                        let vu = 100N |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Some

                        Check.marginedTestRange true (12N / 10N) vu
                        |> maxVal
                        |> Expect.equal "risk: no margin" (Some [| 100.0 |])

                        Check.marginedTestRange false (12N / 10N) vu
                        |> maxVal
                        |> Expect.equal "non-risk: 120%" (Some [| 120.0 |])

                        Check.marginedTestRange false (12N / 10N) vu
                        |> minVal
                        |> Expect.equal "min unchanged" (Some [| 100.0 |])
                    }

                    test "MEDIUM-3 monthsMinMaxToDays at 30 days/month" {
                        let d = Check.monthsMinMaxToDays (mmOf 1N 3N Units.Time.month)
                        d |> minVal |> Expect.equal "1mo -> 30d" (Some [| 30.0 |])
                        d |> maxVal |> Expect.equal "3mo -> 90d" (Some [| 90.0 |])
                    }

                    test "LOW-3 interchangeable frequency time units" {
                        Check.interchangeable "per maand" "per 4 weken"
                        |> Expect.isTrue "maand ~ 4 weken"

                        Check.interchangeable "per 12 weken" "per 3 maanden"
                        |> Expect.isFalse "12 weken != 3 maanden"
                    }

                    test "LOW-4 freqMsg granularity (human description, no IR text code)" {
                        Check.freqMsg true false |> Expect.equal "aantal" "aantal verschilt"

                        Check.freqMsg false true |> Expect.equal "eenheid" "tijdseenheid verschilt"

                        Check.freqMsg true true
                        |> Expect.equal "beide" "aantal en/of tijdseenheid verschilt"
                    }

                    // Regression: the rule's frequency set must be a SUBSET of the
                    // G-Standaard reference (genform ⊆ gstand). The arguments to
                    // ValueUnit.isSubset were once reversed, falsely flagging a rule
                    // that prescribes a subset of the allowed frequencies.
                    test "freqWithinReference checks rule ⊆ reference (not reversed)" {
                        let perDay (vs: BigRational[]) =
                            vs |> ValueUnit.withUnit (Units.Count.times |> ValueUnit.per Units.Time.day)

                        let rule = perDay [| 1N |]
                        let reference = perDay [| 1N; 2N; 3N |]

                        // unitsInterchangeable=false, aantalDiff=true => decision is the subset test only
                        Check.freqWithinReference false true rule reference
                        |> Expect.isTrue "1 x/dag is within the allowed {1,2,3} x/dag"

                        Check.freqWithinReference false true reference rule
                        |> Expect.isFalse "{1,2,3} x/dag is NOT within {1} x/dag"
                    }

                    test "HIGH-2 rateScopeLabel labels or drops" {
                        Check.rateScopeLabel Check.LabelRateChecksOutOfScope "x"
                        |> Expect.equal "label" (Some "[buiten G-Standaard doseringscontrole] x")

                        Check.rateScopeLabel Check.DropRateChecks "x" |> Expect.equal "drop" None
                    }

                    test "BUG-B maximizeDosages merges Abs from Abs (not Norm)" {
                        [ mkDosage 3N 5N; mkDosage 3N 9N ]
                        |> Check.maximizeDosages
                        |> Option.bind _.SingleDosage.Abs.Max
                        |> Option.map (Limit.getValueUnit >> ValueUnit.getValue >> Array.map BigRational.toDouble)
                        |> Expect.equal "Abs.Max 9 (was 3)" (Some [| 9.0 |])
                    }

                    test "BUG-A per-m2 absolute single-dose limit is picked up" {
                        // mirrors createMapping.quantityAdjustAbs: both ranges from
                        // SingleDosage (the m2 branch previously read StartDosage).
                        let single = { ZFDR.DoseRange.empty with AbsBSA = (mkMgMax 100N, Units.BSA.m2) }

                        Check.pickAdjust (single.AbsWeight |> fst) (single.AbsBSA |> fst) convKg convM2
                        |> maxVal
                        |> Expect.equal "picks m2 abs" (Some [| 100.0 |])
                    }

                    test "HIGH-1 maximizeDosages ORs HighRisk across merged dosages" {
                        // First dosage not high risk, a later one is: the merge must
                        // stay high risk so the margin is suppressed (HIGH-1 safety).
                        [ mkRiskDosage false; mkRiskDosage true ]
                        |> Check.maximizeDosages
                        |> Option.map _.HighRisk
                        |> Expect.equal "merged stays high risk" (Some true)
                    }

                    test "UNIT-GUARD rangesComparable true for same unit group (mg/kg/day)" {
                        let perKgDay u =
                            u |> ValueUnit.per Units.Weight.kiloGram |> ValueUnit.per Units.Time.day

                        Check.rangesComparable
                            (mmOf 1N 5N (Units.Mass.milliGram |> perKgDay))
                            (mmOf 2N 3N (Units.Mass.milliGram |> perKgDay))
                        |> Expect.isTrue "same group comparable"
                    }

                    test "UNIT-GUARD rangesComparable false for Count/kg/day vs IU/kg/week" {
                        let perKg u =
                            u |> ValueUnit.per Units.Weight.kiloGram

                        let countKgDay = Units.Count.times |> perKg |> ValueUnit.per Units.Time.day
                        let iuKgWeek = Units.InterNational.iu |> perKg |> ValueUnit.per Units.Time.week

                        Check.rangesComparable (mmOf 50N 50N countKgDay) (mmOf 150N 150N iuKgWeek)
                        |> Expect.isFalse "different groups not comparable"
                    }

                    test "UNIT-GUARD rangesComparable false for droplet vs mg" {
                        Check.rangesComparable (mmOf 1N 1N Units.Volume.droplet) (mmOf 1N 1N Units.Mass.milliGram)
                        |> Expect.isFalse "droplet vs mass not comparable"
                    }

                    test "UNIT-GUARD rangesComparable true when a range is empty" {
                        Check.rangesComparable MinMax.empty (mmOf 1N 5N Units.Mass.milliGram)
                        |> Expect.isTrue "empty ref treated as comparable (empty short-circuit applies)"
                    }

                    test "UNIT-GUARD rangeUnit None on empty, Some on populated" {
                        Check.rangeUnit MinMax.empty |> Expect.isNone "empty"
                        Check.rangeUnit (mmOf 1N 5N Units.Mass.milliGram) |> Expect.isSome "populated"
                    }

                    test "DI checkDoseRuleWithProvider uses the injected provider (no GStand IO)" {
                        // A fake GStandProvider returning no dosages proves the dose
                        // check runs purely off its injected dependency: the seam is
                        // exercised (call count > 0) and no real data access occurs.
                        let mutable calls = 0

                        let fakeProvider: Check.GStandProvider =
                            fun _ _ _ _ ->
                                calls <- calls + 1
                                Seq.empty

                        let result =
                            Check.checkDoseRuleWithProvider fakeProvider Patient.patient sampleDoseRule.Value

                        calls > 0 |> Expect.isTrue "injected provider was invoked"

                        result.didNotPass
                        |> Array.isEmpty
                        |> Expect.isTrue "empty provider data yields no dose-check signals"
                    }
                ]


    /// External formulary links (issue #529): `Source.getLink` is pure over an NKF index
    /// that may be empty, and `SourceLoader` is the IO leaf that must fail as a `Result`.
    module SourceLinkTests =

        let private fk = Source.identified "FK"
        let private nkf = Source.identified "NKF"
        let private label = GenericLabel.fromShorthand

        let private meds: Source.NKFMedication list =
            [
                {
                    Generic = "paracetamol"
                    Id = "123"
                }
                {
                    Generic = "amoxicilline + clavulaanzuur"
                    Id = "456"
                }
            ]

        let tests =
            testList
                "Source links"
                [
                    test "FK link files the generic under its first letter" {
                        // Verified live: .../preparaatteksten/n/paracetamol is a 404,
                        // .../preparaatteksten/p/paracetamol is the monograph.
                        let exp =
                            "[Farmacotherapeutisch Kompas](https://www.farmacotherapeutischkompas.nl/bladeren/preparaatteksten/p/paracetamol#doseringen)"

                        Source.getLink [] fk (label "paracetamol")
                        |> Expect.equal $"FK link should be {exp}" (Some exp)
                    }

                    test "FK link is lower case and slugs '/' to '-'" {
                        let exp =
                            "[Farmacotherapeutisch Kompas](https://www.farmacotherapeutischkompas.nl/bladeren/preparaatteksten/a/amoxicilline-clavulaanzuur#doseringen)"

                        Source.getLink meds fk (label "Amoxicilline/Clavulaanzuur")
                        |> Expect.equal $"FK link should be {exp}" (Some exp)
                    }

                    test "brand- and form-qualified labels link on the base generic" {
                        let fkExp =
                            "[Farmacotherapeutisch Kompas](https://www.farmacotherapeutischkompas.nl/bladeren/preparaatteksten/p/paracetamol#doseringen)"

                        let nkfExp =
                            "[Kinderformularium](https://www.kinderformularium.nl/geneesmiddel/123/paracetamol)"

                        let brand = GenericLabel.fromGenericBrand "paracetamol" "Panadol"
                        let form = GenericLabel.fromGenericForm "paracetamol" "zetpil"

                        Source.getLink meds fk brand |> Expect.equal "FK, brand qualified" (Some fkExp)
                        Source.getLink meds fk form |> Expect.equal "FK, form qualified" (Some fkExp)

                        Source.getLink meds nkf brand
                        |> Expect.equal "NKF, brand qualified" (Some nkfExp)

                        Source.getLink meds nkf form |> Expect.equal "NKF, form qualified" (Some nkfExp)
                    }

                    test "FK link does not need the NKF index" {
                        Source.getLink [] fk (label "paracetamol")
                        |> Expect.equal
                            "same link with and without index"
                            (Source.getLink meds fk (label "paracetamol"))
                    }

                    test "FK link is None for an empty label" {
                        Source.getLink [] fk (label "") |> Expect.isNone "no page for an empty generic"
                    }

                    test "NKF link is None when the index is empty" {
                        Source.getLink [] nkf (label "paracetamol")
                        |> Expect.isNone "empty index (site unreachable) drops NKF links"
                    }

                    test "NKF link is built from the index id and the slugged label" {
                        let exp =
                            "[Kinderformularium](https://www.kinderformularium.nl/geneesmiddel/456/amoxicilline-clavulaanzuur)"

                        Source.getLink meds nkf (label "amoxicilline/clavulaanzuur")
                        |> Expect.equal $"NKF link should be {exp}" (Some exp)
                    }

                    test "NKF index match is case-insensitive" {
                        Source.getLink meds nkf (label "Paracetamol")
                        |> Option.map (String.contains "/geneesmiddel/123/")
                        |> Expect.equal "matched entry 123" (Some true)
                    }

                    test "NKF link is None when the generic is not in the index" {
                        Source.getLink meds nkf (label "ibuprofen") |> Expect.isNone "not indexed"
                    }

                    test "other sources have no link" {
                        Source.getLink meds (Source.other "Lokaal") (label "paracetamol")
                        |> Expect.isNone "no external formulary"
                    }

                    test "noLinks never finds a link" {
                        Source.noLinks fk (label "paracetamol") |> Expect.isNone "FK"
                        Source.noLinks nkf (label "paracetamol") |> Expect.isNone "NKF"
                    }

                    test "fetchNKFMedicationsFrom returns Error, not an exception, when unreachable" {
                        // Port 9 (discard) on the loopback: refused immediately, no DNS.
                        let url = "http://127.0.0.1:9/geneesmiddelen.json"

                        match SourceLoader.fetchNKFMedicationsFrom url with
                        | Ok _ -> failwith "expected Error"
                        | Error [ ErrorMsg(text, Some _) ] ->
                            text |> String.contains url |> Expect.isTrue "message names the url"

                            text
                            |> String.contains "could not fetch"
                            |> Expect.isTrue "message says what failed"
                        | Error msgs -> failwith $"expected one ErrorMsg with the exception, got %A{msgs}"
                    }
                ]


    /// The boxed resource registry engine (approach a) + GStand as a
    /// function-valued resource. Engine validated in Scripts/ResourcesRegistryImpl.fsx
    /// (incl. live load parity); these are the in-CI unit checks.
    module ResourceRegistryTests =

        open Informedica.GenForm.Lib.Resources

        let private kInt name = ResourceKey.create<int> name

        let tests =
            testList
                "Resource registry (boxed engine)"
                [
                    test "typed round-trip: Resolve returns the registered value at 'T" {
                        let k = ResourceKey.create<string[]> "rt"
                        let reg = Map [ k.Name, ofResult (fun () -> Ok [| "a"; "b" |]) ]
                        let v: string[] = LoadEngine(reg).Resolve k
                        v |> Expect.equal "round-trips" [| "a"; "b" |]
                    }

                    test "memoisation: a shared dependency loads once" {
                        let mutable n = 0
                        let leaf = kInt "leaf"
                        let a = kInt "a"
                        let b = kInt "b"

                        let reg =
                            Map
                                [
                                    leaf.Name,
                                    ofResult (fun () ->
                                        n <- n + 1
                                        Ok 1
                                    )
                                    a.Name, derive (fun r -> r.Get leaf + 1)
                                    b.Name, derive (fun r -> r.Get leaf + 2)
                                ]

                        let eng = LoadEngine reg
                        eng.Resolve a |> ignore
                        eng.Resolve b |> ignore
                        n |> Expect.equal "leaf loaded once" 1
                    }

                    test "cycle is detected" {
                        let a = kInt "ca"
                        let b = kInt "cb"

                        let reg =
                            Map
                                [
                                    a.Name, derive (fun r -> r.Get b)
                                    b.Name, derive (fun r -> r.Get a)
                                ]

                        let threw =
                            try
                                LoadEngine(reg).Resolve a |> ignore
                                false
                            with ResourceLoadError _ ->
                                true

                        threw |> Expect.isTrue "cyclic dependency raises"
                    }

                    test "fatal leaf error aborts the load" {
                        let k = kInt "bad"
                        let reg = Map [ k.Name, (fun _ -> Error [ ErrorMsg("boom", None) ]) ]

                        let threw =
                            try
                                LoadEngine(reg).Resolve k |> ignore
                                false
                            with ResourceLoadError es ->
                                es = [ ErrorMsg("boom", None) ]

                        threw |> Expect.isTrue "fatal error raises ResourceLoadError"
                    }

                    test "GStand is a function-valued resource (no ZIndex IO)" {
                        let mutable called = false

                        let fake: Check.GStandProvider =
                            fun _ _ _ _ ->
                                called <- true
                                []

                        let reg = Map [ Keys.gStandProvider.Name, derive (fun _ -> fake) ]
                        let gstand: Check.GStandProvider = LoadEngine(reg).Resolve Keys.gStandProvider
                        let out = gstand Patient.patient "paracetamol" "tablet" "iv"

                        out |> Seq.isEmpty |> Expect.isTrue "fake provider returns empty"
                        called |> Expect.isTrue "function-valued resource was invoked"
                    }

                    test "ofResultOrDefault: Ok passes the value through without warnings" {
                        let k = kInt "opt-ok"
                        let reg = Map [ k.Name, ofResultOrDefault "note" -1 (fun () -> Ok 42) ]
                        let eng = LoadEngine reg

                        eng.Resolve k |> Expect.equal "value" 42
                        eng.Warnings |> Expect.equal "no warnings" []
                    }

                    test "ofResultOrDefault: Error yields the fallback and a prefixed Warning per error" {
                        let k = kInt "opt-err"

                        let reg =
                            Map
                                [
                                    k.Name,
                                    ofResultOrDefault
                                        "not loaded"
                                        -1
                                        (fun () -> Error [ ErrorMsg("boom", None); Warning "meh" ])
                                ]

                        let eng = LoadEngine reg

                        eng.Resolve k |> Expect.equal "fallback served" -1

                        eng.Warnings
                        |> Expect.equal
                            "each error is a Warning prefixed with the note"
                            [ Warning "not loaded: boom"; Warning "not loaded: meh" ]
                    }

                    test "nkfLinkProvider degrades to FK-only links when the fetch fails" {
                        // Mirrors the defaultRegistry entry, with the fetch replaced.
                        let reg =
                            Map
                                [
                                    Keys.nkfLinkProvider.Name,
                                    ofResultOrDefault
                                        "NKF links not loaded"
                                        (Source.getLink []: Source.LinkProvider)
                                        (fun () -> Error [ ErrorMsg("kinderformularium.nl down", None) ])
                                ]

                        let eng = LoadEngine reg
                        let getLink: Source.LinkProvider = eng.Resolve Keys.nkfLinkProvider
                        let label = GenericLabel.fromShorthand "paracetamol"

                        getLink (Source.identified "FK") label |> Expect.isSome "FK link survives"

                        getLink (Source.identified "NKF") label |> Expect.isNone "NKF link is lost"

                        eng.Warnings
                        |> Expect.equal
                            "outage surfaces as a Warning"
                            [
                                Warning "NKF links not loaded: kinderformularium.nl down"
                            ]
                    }
                ]


    [<Tests>]
    let tests =
        testList
            "GenForm Tests"
            [
                DoseLimitTests.tests
                AdjustDoseLimitTests.tests
                MaxQtyConflictTests.tests
                PatientCategoryTests.tests
                DoseTypeTests.tests
                LimitTargetTests.tests
                GenericLabelTests.tests
                ProductFilterTests.tests
                DoseRuleProductTests.tests
                DoseRuleToDataTests.tests
                DoseRuleRoundtripTests.tests
                CheckTests.tests
                SourceLinkTests.tests
                ResourceRegistryTests.tests
            ]
