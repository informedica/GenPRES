

#time

#load "load.fsx"

open System
let dataUrlId = "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I"
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", dataUrlId)


#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../Patient.fs"
#load "../LimitTarget.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"
#load "../Check.fs"
#load "../SolutionRule.fs"
#load "../RenalRule.fs"
#load "../PrescriptionRule.fs"
#load "../FormLogging.fs"
#load "../Api.fs"


open MathNet.Numerics
open FsToolkit.ErrorHandling
open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib


module GenFormResult =

    let defaultValue value res =
        res
        |> Result.map fst
        |> Result.defaultValue value


let provider : Resources.IResourceProvider = Api.getCachedProviderWithDataUrlId FormLogging.ignore dataUrlId


// Usage
provider.GetProducts ()
|> Array.filter (fun p -> p.Generic = "nitroprusside")


let dr =
    provider.GetDoseRules ()
    |> Api.filterDoseRules provider
        { Filter.doseFilter with
            Patient =
                { Patient.patient with
                    Locations =  [ CVL ]
                    Department = Some "ICK"
                    Age =
                        Units.Time.year
                        |> ValueUnit.singleWithValue 1N
                        |> Some
                    Weight =
                      Units.Weight.kiloGram
                      |> ValueUnit.singleWithValue 10N
                      |> Some
                }
            Indication = Some "Sedatie op de IC"
            Generic = Some "midazolam"
            Shape = None
            Route = Some "INTRAVENEUS"
        }
    |> Array.head



dr
|> DoseRule.addShapeLimits
    (provider.GetRouteMappings ())
    (provider.GetShapeRoutes ())


let doseRuleData =
    DoseRule.getData dataUrlId
    |> GenFormResult.defaultValue [||]


doseRuleData
|> Array.filter (fun dr ->
    dr.Generic = "Samenstelling C" &&
    dr.DoseType = "timed" &&
    dr.DoseText = "dag 1"
)




provider.GetSolutionRules ()
|> Array.filter (fun sr ->
    sr.Generic = "amikacine" &&
    sr.Route =  "intraveneus" &&
    sr.PatientCategory.Department = Some "ICK"
)


let pr =
    { Filter.doseFilter with
        Indication = Some "Sedatie op de IC"
        Generic = Some "midazolam"
        Route = Some "intraveneus"
        DoseType = Continuous "" |> Some
        Patient =
            { Patient.patient with
                Department = Some "ICK"
                Age =
                    Units.Time.year
                    |> ValueUnit.singleWithValue 10N
                    |> Some
                Weight =
                  Units.Weight.kiloGram
                  |> ValueUnit.singleWithValue 40N
                  |> Some
//                RenalFunction = EGFR(Some 5, Some 5) |> Some
            }
    }
    |> Api.filterPrescriptionRules provider
    |> GenFormResult.defaultValue [||]
    |> Array.head


pr.SolutionRules


{ Patient.patient with
    Locations =  [ CVL ]
    Department = Some "ICK"
    Age =
        Units.Time.year
        |> ValueUnit.singleWithValue 6N
        |> Some
    Weight =
      Units.Weight.kiloGram
      |> ValueUnit.singleWithValue 22N
      |> Some
//                RenalFunction = EGFR(Some 5, Some 5) |> Some
}
|> Api.getPrescriptionRules provider
|> GenFormResult.defaultValue [||]
|> Array.last
|> fun pr -> pr.DoseRule.ComponentLimits |> Array.toList


let printAllDoseRules () =
    let rs =
        Filter.doseFilter
        |> Api.filterPrescriptionRules provider
        |> GenFormResult.defaultValue [||]
        |> Array.map _.DoseRule

    let gs (rs : DoseRule[]) =
        rs
        |> Array.map _.Generic
        |> Array.distinct

    DoseRule.Print.printGenerics gs rs

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

printAllDoseRules ()
|> String.concat "\n\n  ---\n"
|> Informedica.ZForm.Lib.Markdown.toHtml
|> File.writeTextToFile "doserules.html"


provider.GetSolutionRules ()
|> Array.filter (fun sr -> sr.Generic = "adrenaline")
|> SolutionRule.Print.toMarkdown ""


{ Patient.patient with
    Age =
        Units.Time.year
        |> ValueUnit.singleWithValue 16N
        |> Some
}
|> Api.getPrescriptionRules provider
|> GenFormResult.defaultValue [||]
|> Array.filter (_.SolutionRules >> Array.isEmpty >> not)
|> Array.item 1
|> fun pr ->
    pr.SolutionRules
    |> SolutionRule.Print.toMarkdown "\n"


provider.GetDoseRules ()
|> DoseRule.filter
    (provider.GetRouteMappings ())
    { Filter.doseFilter with
        Route = Some "oraal"
    }