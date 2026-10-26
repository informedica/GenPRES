namespace Informedica.GenPRES.Client.Core.Models

/// The intake totals as the pages show them, one row per substance.
module TotalsDisplay =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    /// The intake rows: the substance, an empty value and the unit.
    let intakeRows =
        [|
            [| "volume"; ""; "ml/kg/dag" |]
            [| "energie"; ""; "kCal/kg/dag" |]
            [| "koolhydraat"; ""; "mg/kg/min" |]
            [| "eiwit"; ""; "g/kg/dag" |]
            [| "vet"; ""; "g/kg/dag" |]
            [| "natrium"; ""; "mmol/kg/dag" |]
            [| "kalium"; ""; "mmol/kg/dag" |]
            [| "chloride"; ""; "mmol/kg/dag" |]
            [| "calcium"; ""; "mmol/kg/dag" |]
            [| "magnesium"; ""; "mmol/kg/dag" |]
            [| "fosfaat"; ""; "mmol/kg/dag" |]
            [| "ijzer"; ""; "mmol/kg/dag" |]
            [| "vit D"; ""; "mmol/kg/dag" |]
            [| "ethanol"; ""; "mg/kg/dag" |]
            [| "propyleenglycol"; ""; "mg/kg/dag" |]
            [| "boorzuur"; ""; "mmol/kg/dag" |]
            [| "benzylalcohol"; ""; "mmol/kg/dag" |]
        |]


    /// The totals of the substance a row names.
    let substanceToField (intake: Totals) =
        function
        | "volume" -> intake.Volume
        | "energie" -> intake.Energy
        | "koolhydraat" -> intake.Carbohydrate
        | "eiwit" -> intake.Protein
        | "vet" -> intake.Fat
        | "natrium" -> intake.Sodium
        | "kalium" -> intake.Potassium
        | "chloride" -> intake.Chloride
        | "calcium" -> intake.Calcium
        | "magnesium" -> intake.Magnesium
        | "phosphaat"
        | "fosfaat" -> intake.Phosphate
        | "ijzer" -> intake.Iron
        | "vitamine D"
        | "vit D" -> intake.VitaminD
        | "ethanol" -> intake.Ethanol
        | "propyleenglycol" -> intake.Propyleenglycol
        | "boorzuur" -> intake.BoricAcid
        | "benzylalcohol" -> intake.BenzylAlcohol
        | _ -> [||]
