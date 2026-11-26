/// The parenteralia request selects the solution rules for its patient and department.
module Informedica.GenPRES.Server.Tests.ParenteraliaServiceTests

open System
open Expecto
open Expecto.Flip
// before the GenFORM types, whose DoseType must win; the measures of the patient come from here
open Informedica.GenPRES.Shared.Types
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Types
open Informedica.GenCore.Lib.Ranges
open ServerApi
open Informedica.GenPRES.Server.Tests.TestDates

module SharedModels = Informedica.GenPRES.Shared.Models


/// A morfine rule for the department dep, or for every department when dep is None.
let morfine dep : SolutionRule =
    {
        Id = ""
        SourceText = ""
        GPKs = [||]
        HPKs = [||]
        ShelfLife = None
        MinPh = None
        MaxPh = None
        SameLine = [||]
        NotSameLine = [||]
        Notes = ""
        PrepText = ""
        AdminText = ""
        Generic = "morfine"
        Form = None
        Route = "INTRAVENEUS"
        Indication = None
        DoseType = NoDoseType
        PatientCategory = { PatientCategory.empty with Department = dep }
        Dose = MinMax.empty
        AdjustUnit = None
        Diluents = [||]
        SolutionLimit = SolutionRule.SolutionLimit.empty
        DilutionLimits = [||]
    }


/// A provider with a morfine rule for NEO, one for ICK and one for every department.
let provider =
    TestProviders.SolutionRulesProvider [| morfine (Some "NEO"); morfine (Some "ICK"); morfine None |]


/// A newborn of one week and 3 kg at the department dep.
let newborn dep =
    { SharedModels.PatientData.empty with
        Age =
            Some
                {
                    Years = 0<year>
                    Months = 0<month>
                    Weeks = 1<week>
                    Days = 0<day>
                }
        Weight = { SharedModels.PatientData.empty.Weight with Measured = Some 3000<gram> }
        Department = dep
    }


/// The markdown of the parenteralia for morfine and the patient.
let markdown patient =
    { SharedModels.Parenteralia.empty with
        Generic = Some "morfine"
        PatientData = patient
    }
    |> ParenteraliaService.get readAt Informedica.Logging.Lib.Logging.noOp provider
    |> Result.map _.Markdown


/// The departments in the markdown of the parenteralia for morfine and the patient.
let departments patient =
    patient
    |> markdown
    |> Result.map (fun md -> [ "NEO"; "ICK" ] |> List.filter md.Contains)


[<Tests>]
let tests =
    testList
        "ParenteraliaService.get"
        [
            test "without a patient, the rules of every department are shown" {
                None |> departments |> Expect.equal "both departments" (Ok [ "NEO"; "ICK" ])
            }

            test "a patient at NEO gets the NEO rule and not the ICK rule" {
                newborn (Some "NEO")
                |> Some
                |> departments
                |> Expect.equal "only NEO" (Ok [ "NEO" ])
            }

            test "a patient without a department gets only the rule that names no department" {
                newborn None |> Some |> departments |> Expect.equal "no department" (Ok [])

                newborn None
                |> Some
                |> markdown
                |> Result.map String.IsNullOrWhiteSpace
                |> Expect.equal "the rule is shown" (Ok false)
            }

            test "a patient without age or weight gives the error, as the formulary does" {
                { SharedModels.PatientData.empty with Department = Some "NEO" }
                |> Some
                |> departments
                |> Expect.isError "not enough patient data"
            }
        ]
