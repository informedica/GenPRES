// Step 2.3 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #745):
// the five nutrition rule sets as domain values, `NutritionRuleSet[]`, owned by the server as
// configuration and handed to the order plan rules by the composition root. Today the same
// five sets live in the nutrition service as its own record type; that copy goes when the
// order plan port moves onto domain values (step 4.3). The label of each category comes along,
// so that the words for a refused category stay the server's. The old record's dose types were
// empty in every set and have no counterpart on the domain type.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. A new src/Informedica.GenPRES.Server/ServerApi.NutritionRuleSets.fs, after
//      ServerApi.Ports.fs in the fsproj, holding the module below.
//   2. A new tests/Informedica.GenPRES.Server.Tests/NutritionRuleSetsTests.fs with the tests
//      below; the golden test against the service's copy goes with that copy in step 4.3.
//
// Run from this directory: dotnet fsi NutritionRuleSets.fsx

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open Informedica.GenOrder.Lib


/// The nutrition rule sets: what each nutrition category draws from, the indications and the
/// generics its dose rules are filtered on, and the label shown for it. Configuration the
/// server owns; the composition root hands `all` to the order plan rules.
module NutritionRuleSets =

    let enteralFeeding: NutritionRuleSet =
        {
            Category = NutritionCategory.EnteralFeeding
            Label = "Enterale Voeding"
            Indications = [| "Enterale voeding" |]
            Generics =
                [|
                    "Infatrini"
                    "Nutrini"
                    "Nutrini Energy"
                    "Nutrini Energy Multi Fibre"
                    "Nutrini Multi Fibre"
                    "Nutrison"
                    "Nutrison Energy"
                    "Nutrison Energy Multi Fibre"
                    "Nutrison Multi Fibre"
                    "Nutrison Protein Plus"
                    "Nutrison Protein Plus Multi Fibre"
                    "Peptisorb"
                    "Peptisorb Plus"
                    "Moedermelk"
                    "Nutrilon Premature"
                    "Nutrilon Nenatal Start"
                    "Nutrilon Nenatal 1"
                |]
        }


    let enteralSupplement: NutritionRuleSet =
        {
            Category = NutritionCategory.EnteralSupplement
            Label = "Enteraal Supplement"
            Indications = [| "Enterale toevoeging" |]
            Generics =
                [|
                    "Calogen neutraal pdr"
                    "Fantomalt pdr"
                    "Hero Baby 1 NS Comfort pdr"
                    "Hero Baby 1 NS Pep pdr"
                    "Hero Baby 1 NS Standaard pdr"
                    "Hero Baby 2 NS Comfort pdr"
                    "Hero Baby 2 NS Pep pdr"
                    "Hero Baby 2 NS Standaard pdr"
                    "Liquigen pdr"
                    "Neocate Junior pdr"
                    "Neocate LCP pdr"
                    "Nutramigen 1 LGG pdr"
                    "Nutramigen 2 LGG pdr"
                    "Nutrilon 1 pdr"
                    "Nutrilon Hypoallergeen 1 pdr"
                    "Nutrilon Nenatal 1 pdr"
                    "Nutrilon Nenatal BMF pdr"
                    "Nutrilon Nenatal Protein Fortifier pdr"
                    "Nutrilon Nenatal Start pdr"
                    "Nutrilon Pepti 1 pdr"
                    "Nutrilon Pepti Junior pdr"
                    "Nutrison Advanced Peptisorb pdr"
                    "Nutriton pdr"
                |]
        }


    let tpn: NutritionRuleSet =
        {
            Category = NutritionCategory.TPN
            Label = "Totale Parenterale Voeding"
            Indications =
                [|
                    "Standaard Totale Parenterale Voeding"
                    "Variabele Totale Parenterale Voeding"
                    "Neonatale Parenterale Voeding"
                    "Totale Parenterale Voeding"
                |]
            Generics =
                [|
                    "Primene"
                    "NICU Mix"
                    "Samenstelling B"
                    "Samenstelling C"
                    "Samenstelling D"
                    "Samenstelling E"
                    "Numeta G13%E 2CZ"
                    "Numeta G13%E 3CZ"
                    "Numeta G16%E 2CZ"
                    "Numeta G16%E 3CZ"
                    "Numeta G19%E 2CZ"
                    "Numeta G19%E 3CZ"
                |]
        }


    let lipid: NutritionRuleSet =
        {
            Category = NutritionCategory.Lipid
            Label = "Vetten"
            Indications = [| "Parenterale vetten" |]
            Generics = [| "Intralipid 20%"; "SMOFlipid 20%" |]
        }


    let electrolyteGlucose: NutritionRuleSet =
        {
            Category = NutritionCategory.ElectrolyteGlucose
            Label = "Elektrolyten/Glucose"
            Indications = [| "Parenterale suppletie" |]
            Generics =
                [|
                    "NaCl 0,9%"
                    "NaCl 3%"
                    "Glucose 5%"
                    "Glucose 10%"
                    "Glucose 20%"
                    "Glucose 50%"
                    "calciumglubionat/calciumgluconaat"
                    "KCl 7,4%"
                    "KCl"
                    "NaCl"
                    "magnesiumsulfaat"
                    "fosfaat"
                    "calciumgluconaat"
                |]
        }


    /// Every set, one per category, for the composition root.
    let all: NutritionRuleSet[] =
        [| enteralFeeding; enteralSupplement; tpn; lipid; electrolyteGlucose |]


// ---------------------------------------------------------------------------
// Tests, for tests/Informedica.GenPRES.Server.Tests/NutritionRuleSetsTests.fs
// ---------------------------------------------------------------------------

module NutritionRuleSetsTests =

    open Expecto
    open Expecto.Flip


    let categories =
        [
            NutritionCategory.EnteralFeeding
            NutritionCategory.EnteralSupplement
            NutritionCategory.TPN
            NutritionCategory.Lipid
            NutritionCategory.ElectrolyteGlucose
        ]


    /// The contract model's category, for the service's copy and the client's label.
    let sharedCategory category =
        match category with
        | NutritionCategory.EnteralFeeding -> Shared.Types.NutritionCategory.EnteralFeeding
        | NutritionCategory.EnteralSupplement -> Shared.Types.NutritionCategory.EnteralSupplement
        | NutritionCategory.TPN -> Shared.Types.NutritionCategory.TPN
        | NutritionCategory.Lipid -> Shared.Types.NutritionCategory.Lipid
        | NutritionCategory.ElectrolyteGlucose -> Shared.Types.NutritionCategory.ElectrolyteGlucose


    /// The service's copy of a category, until step 4.3 removes it.
    let serviceCopy category =
        category |> sharedCategory |> ServerApi.NutritionPlanService.getDoseRuleSet


    let tests =
        testList
            "nutrition rule sets"
            [
                test "one set per category, every category served" {
                    NutritionRuleSets.all
                    |> Array.map _.Category
                    |> Array.toList
                    |> List.sort
                    |> Expect.equal "each once" (categories |> List.sort)

                    for category in categories do
                        NutritionRuleSets.all
                        |> NutritionRuleSet.tryFind category
                        |> Option.map _.Category
                        |> Expect.equal $"{category} found" (Some category)
                }

                test "each set says what the service's copy says" {
                    for category in categories do
                        let set = NutritionRuleSets.all |> NutritionRuleSet.tryFind category |> Option.get
                        let copy = serviceCopy category
                        set.Label |> Expect.equal $"{category} label" copy.Label
                        set.Indications |> Expect.equal $"{category} indications" copy.Indications
                        set.Generics |> Expect.equal $"{category} generics" copy.Generics
                        copy.DoseTypes |> Expect.isEmpty $"{category}: no dose types to carry"
                }

                test "each label is the one the client shows for the category" {
                    for category in categories do
                        let set = NutritionRuleSets.all |> NutritionRuleSet.tryFind category |> Option.get

                        set.Label
                        |> Expect.equal
                            $"{category} label"
                            (category |> sharedCategory |> Shared.Models.NutritionCategory.label)
                }

                test "no set is empty: every category names at least one indication and one generic" {
                    for set in NutritionRuleSets.all do
                        set.Indications |> Expect.isNonEmpty $"{set.Category} indications"
                        set.Generics |> Expect.isNonEmpty $"{set.Category} generics"
                }
            ]


NutritionRuleSetsTests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||] |> ignore
