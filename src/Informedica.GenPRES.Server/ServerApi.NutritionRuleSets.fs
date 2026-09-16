namespace ServerApi

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
        [|
            enteralFeeding
            enteralSupplement
            tpn
            lipid
            electrolyteGlucose
        |]
