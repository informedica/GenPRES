// What no department means. Four patient-side matchers compare a patient's department with a
// rule's, and all four pass when either side is absent: `PatientCategory.filter`, which the
// dose-rule filter applies; `PatientCategory.filterPatient`, which the solution rules apply;
// `Reconstitution.filter`; and the predicate inside `Product.reconstitute`. So a patient with no
// department matches every rule of every department, one hospital's ward rules included. Today
// the `ICK` default in the server's mapper and in the MCP host is the only thing in the way.
//
// On the live sheets no dose rule names a department, while nine in ten solution rules and
// reconstitutions do, so the fault is in the infusion and reconstitution constraints, not the
// doses. The default stays until the user can choose; but before a department can be chosen, or
// cleared, the matchers must say what a missing one means.
//
// The rule, one predicate for all four sites: a rule that names no department is a rule for
// everybody; a patient with no department matches no rule that names one; a patient with one
// matches the rules that name it and the rules that name none. The location's comparison is
// left as it is, since nothing sets a location today.
//
// Script-first draft (script-only policy): `departmentMatches` and the two category matchers
// → `GenFORM.Lib/Patient.fs`, `Reconstitution.matches` and `filter` → `GenFORM.Lib/Product.fs`,
// where `reconstitute`'s inline predicate becomes a call of `matches`; the tests →
// `tests/Informedica.GenFORM.Tests/Tests.fs`.
//
// Run: `dotnet fsi Department.fsx` from this directory. The last test list reads the live
// sheets through `GENPRES_URL_ID` from `.env`, and is skipped when that is not set.

#load "load.fsx"

open System

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore

#load "../Types.fs"
#load "../Utils.fs"
#load "../Logging.fs"
#load "../Mapping.fs"
#load "../Patient.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../LimitTarget.fs"
#load "../DoseLimit.fs"
#load "../DoseType.fs"
#load "../GenericLabel.fs"
#load "../PharmaceuticalForm.fs"
#load "../ProductId.fs"
#load "../Generic.fs"
#load "../Source.fs"
#load "../DoseRule.fs"
#load "../DoseRuleData.fs"
#load "../DoseRuleLoader.fs"
#load "../SourceLoader.fs"
#load "../Check.fs"
#load "../SolutionLimit.fs"
#load "../SolutionRule.fs"
#load "../RenalRule.fs"
#load "../PrescriptionRule.fs"
#load "../FormLogging.fs"
#load "../Resources.fs"

open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib


module PatientCategory =

    open Informedica.GenForm.Lib.PatientCategory

    module MinMax = Informedica.GenCore.Lib.Ranges.MinMax

    open Utils


    /// Whether a patient's department is one a rule applies to. A rule that names no
    /// department is a rule for everybody; a patient with no department matches no rule that
    /// names one; a patient with one matches the rules that name it.
    let departmentMatches (rule: string option) (patient: string option) =
        match rule, patient with
        | None, _ -> true
        | Some _, None -> false
        | Some rule, Some patient -> rule = patient


    /// → `PatientCategory.filter`: the department line replaced, the rest as it is.
    let filter (filter: DoseFilter) (patCat: PatientCategory) =
        let eqs a b =
            match a, b with
            | None, _
            | _, None -> true
            | Some a, Some b -> a = b

        ([| patCat |],
         [|
             // if either filter location or patient category location is None, pass; otherwise must match
             fun (p: PatientCategory) -> filter.Patient.Location |> eqs p.Location
             // a rule that names a department is for the patients of that department only
             fun (p: PatientCategory) -> filter.Patient.Department |> departmentMatches p.Department
             // patient age must be within patient category age range (if specified)
             fun (p: PatientCategory) -> filter.Patient.Age |> MinMax.inRange (p |> getAge)
             // patient weight must be within patient category weight range (if specified)
             fun (p: PatientCategory) -> filter.Patient.Weight |> MinMax.inRange p.Weight
             // if both weight and height are given, calculate BSA and check if within patient category BSA range
             fun (p: PatientCategory) ->
                 match filter.Patient.Weight, filter.Patient.Height with
                 | Some w, Some h -> Calculations.calcDuBois w h |> Some |> MinMax.inRange p.BSA
                 | _ -> true
             if filter.Patient.Age |> Option.isSome then
                 yield!
                     [|
                         fun (p: PatientCategory) ->
                             if p |> isEmpty then
                                 true
                             else if
                                 filter.Patient.GestAge.IsSome
                                 && p.GestAge = MinMax.empty
                                 && p.PMAge = MinMax.empty
                             then
                                 filter.Patient.GestAge.Value >=? ValueUnit.ageFullTerm
                             else
                                 filter.Patient.GestAge
                                 |> Option.defaultValue Utils.ValueUnit.ageFullTerm
                                 |> Some
                                 |> MinMax.inRange p.GestAge
                         fun (p: PatientCategory) ->
                             if p |> isEmpty then
                                 true
                             else
                                 filter.Patient.PMAge
                                 |> Option.defaultValue (filter.Patient.Age.Value + Utils.ValueUnit.ageFullTerm)
                                 |> Some
                                 |> MinMax.inRange p.PMAge
                     |]
             fun (p: PatientCategory) -> filter |> Gender.filter p.Gender
             fun (p: PatientCategory) -> VenousAccess.check p.Access filter.Patient.Access
         |])
        ||> Array.fold (fun acc pred -> acc |> Array.filter pred)
        |> fun xs -> xs |> Array.length > 0


    /// → `PatientCategory.filterPatient`: the department line replaced, the rest as it is.
    let filterPatient (pat: Patient) (patCat: PatientCategory) =
        ([| patCat |],
         [|
             fun (p: PatientCategory) -> pat.Department |> departmentMatches p.Department
             fun (p: PatientCategory) -> pat.Age |> MinMax.inRange (p |> getAge)
             fun (p: PatientCategory) -> pat.Weight |> MinMax.inRange p.Weight
             fun (p: PatientCategory) ->
                 match pat.Weight, pat.Height with
                 | Some w, Some h -> Calculations.calcDuBois w h |> Some |> MinMax.inRange p.BSA
                 | _ -> true
             if pat.Age |> Option.isSome then
                 yield!
                     [|
                         fun (p: PatientCategory) ->
                             if p |> isEmpty then
                                 true
                             else if pat.GestAge.IsSome && p.GestAge = MinMax.empty && p.PMAge = MinMax.empty then
                                 pat.GestAge.Value >=? Utils.ValueUnit.ageFullTerm
                             else
                                 pat.GestAge
                                 |> Option.defaultValue Utils.ValueUnit.ageFullTerm
                                 |> Some
                                 |> MinMax.inRange p.GestAge
                         fun (p: PatientCategory) ->
                             if p |> isEmpty then
                                 true
                             else
                                 pat.PMAge
                                 |> Option.defaultValue (pat.Age.Value + Utils.ValueUnit.ageFullTerm)
                                 |> Some
                                 |> MinMax.inRange p.PMAge
                     |]
             fun (p: PatientCategory) -> VenousAccess.check p.Access pat.Access
         |])
        ||> Array.fold (fun acc pred -> acc |> Array.filter pred)
        |> fun xs -> xs |> Array.length > 0


module Reconstitution =

    open Informedica.GenForm.Lib.Product.Reconstitution


    /// Whether a reconstitution applies to a patient's location and department and a route:
    /// the route when one is asked for; the location when both sides name one; the department
    /// by the one rule of `PatientCategory.departmentMatches`. This is the predicate
    /// `Reconstitution.filter` applies and the one `Product.reconstitute` applies inline; after
    /// migration both call it.
    let matches routeMapping (loc: string option) (dep: string option) (rte: string option) (r: Reconstitution) =
        let eqsOpt a b =
            match a, b with
            | Some a, Some b -> a = b
            | _ -> true

        r.Route |> Mapping.eqsRoute routeMapping rte
        && r.Location |> eqsOpt loc
        && PatientCategory.departmentMatches r.Department dep


    /// → `Reconstitution.filter`, over `matches`.
    let filter routeMapping (filter: DoseFilter) (rs: Reconstitution[]) =
        rs
        |> Array.filter (matches routeMapping filter.Patient.Location filter.Patient.Department filter.Route)


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


/// The matchers as they are, to show what they do today and that what they did right is kept.
module Original =

    let filter = Informedica.GenForm.Lib.PatientCategory.filter

    let filterPatient = Informedica.GenForm.Lib.PatientCategory.filterPatient

    let reconstitutionFilter = Informedica.GenForm.Lib.Product.Reconstitution.filter


/// A rule's category naming a department alone, as nearly every solution rule and
/// reconstitution on the sheet does.
let categoryOf dep =
    { PatientCategory.empty with
        Department = dep
    }


let patientOf dep =
    { Patient.patient with
        Department = dep
    }


let filterOf dep =
    { Filter.doseFilter with
        Patient = patientOf dep
    }


let reconstitutionOf dep : Reconstitution =
    {
        GPK = "1"
        Route = "iv"
        Location = None
        Department = dep
        DiluentVolume = Units.Volume.milliLiter |> ValueUnit.singleWithValue 10N
        ExpansionVolume = None
        Diluents = [| "NaCl 0,9%" |]
    }


/// Every combination of a rule's department and a patient's, and whether they should match.
let cases =
    [
        "a rule for everybody and a patient with no department", None, None, true
        "a rule for everybody and a patient of ICK", None, Some "ICK", true
        "a rule of ICK and a patient with no department", Some "ICK", None, false
        "a rule of ICK and a patient of ICK", Some "ICK", Some "ICK", true
        "a rule of ICK and a patient of NEO", Some "ICK", Some "NEO", false
    ]


/// The four patient-side matchers, each as a function of the rule's department and the
/// patient's, so the same cases run against all of them.
let matchers =
    [
        "PatientCategory.filter", (fun rule pat -> categoryOf rule |> PatientCategory.filter (filterOf pat))
        "PatientCategory.filterPatient", (fun rule pat -> categoryOf rule |> PatientCategory.filterPatient (patientOf pat))
        "Reconstitution.matches", (fun rule pat -> reconstitutionOf rule |> Reconstitution.matches [||] None pat None)
        "Reconstitution.filter",
        (fun rule pat ->
            [| reconstitutionOf rule |]
            |> Reconstitution.filter [||] (filterOf pat)
            |> Array.isEmpty
            |> not
        )
    ]


let originals =
    [
        "PatientCategory.filter", (fun rule pat -> categoryOf rule |> Original.filter (filterOf pat))
        "PatientCategory.filterPatient",
        (fun rule pat -> categoryOf rule |> Original.filterPatient (patientOf pat))
        "Reconstitution.filter",
        (fun rule pat ->
            [| reconstitutionOf rule |]
            |> Original.reconstitutionFilter [||] (filterOf pat)
            |> Array.isEmpty
            |> not
        )
    ]


let ruleTests =
    testList
        "what no department means"
        [
            testList
                "the matchers as they are: a patient with no department matches a rule of any department"
                [
                    for name, matches in originals do
                        test $"{name}" {
                            matches (Some "ICK") None |> Expect.isTrue "the original passes it"
                        }
                ]

            testList
                "the predicate"
                [
                    for name, rule, pat, expected in cases do
                        test $"{name}" {
                            PatientCategory.departmentMatches rule pat
                            |> Expect.equal (if expected then "matches" else "does not match") expected
                        }
                ]

            testList
                "the four matchers follow the predicate"
                [
                    for mName, matches in matchers do
                        testList
                            mName
                            [
                                for name, rule, pat, expected in cases do
                                    test $"{name}" {
                                        matches rule pat
                                        |> Expect.equal (if expected then "matches" else "does not match") expected
                                    }
                            ]
                ]

            testList
                "what is left as it is"
                [
                    test "a rule that names a location still matches a patient with none" {
                        { categoryOf None with
                            Location = Some "OK"
                        }
                        |> PatientCategory.filter (filterOf None)
                        |> Expect.isTrue "the location comparison is untouched"
                    }

                    test "the department is compared as it is written, as before" {
                        PatientCategory.departmentMatches (Some "ICK") (Some "ick")
                        |> Expect.isFalse "case counts, as it did"
                    }

                    test "a reconstitution still asks for the route when one is given" {
                        reconstitutionOf None
                        |> Reconstitution.matches [||] None None (Some "oraal")
                        |> Expect.isFalse "iv is not oraal"
                    }
                ]
        ]


// ---------------------------------------------------------------------------------------------
// Against the live sheets: the solution rules and the reconstitutions, by the department each
// names, before and after
// ---------------------------------------------------------------------------------------------

let dataUrlId = Environment.GetEnvironmentVariable "GENPRES_URL_ID"


let sheetTests =
    if dataUrlId |> String.isNullOrWhiteSpace then
        testList "the live sheets" [ test "skipped: GENPRES_URL_ID is not set" { () } ]
    else
        let engine = Resources.LoadEngine(Resources.defaultRegistry FormLogging.noOp dataUrlId)

        let solutionRows = engine.Resolve Resources.Keys.solutionRuleData
        let reconstitutions = engine.Resolve Resources.Keys.reconstitution
        let routeMapping = engine.Resolve Resources.Keys.routeMappings

        // a solution rule's category, by its department alone, as the matcher reads it
        let solutionCategories = solutionRows |> Array.map (fun r -> categoryOf r.Department)

        let countSolution matcher pat =
            solutionCategories |> Array.filter (matcher (patientOf pat)) |> Array.length

        let countReconstitution filter pat =
            reconstitutions |> filter routeMapping (filterOf pat) |> Array.length

        let namingNone (deps: string option[]) =
            deps |> Array.filter Option.isNone |> Array.length

        let naming dep (deps: string option[]) =
            deps |> Array.filter (fun d -> d = dep || d.IsNone) |> Array.length

        let solutionDeps = solutionRows |> Array.map _.Department
        let reconstitutionDeps = reconstitutions |> Array.map _.Department

        printfn "Solution rules: %i rows, %i naming no department" solutionRows.Length (namingNone solutionDeps)
        printfn "Reconstitutions: %i rows, %i naming no department" reconstitutions.Length (namingNone reconstitutionDeps)

        for name, pat in [ "no department", None; "ICK", Some "ICK" ] do
            printfn
                "  patient with %s: solution rules %i before, %i after; reconstitutions %i before, %i after"
                name
                (countSolution Original.filterPatient pat)
                (countSolution PatientCategory.filterPatient pat)
                (countReconstitution Original.reconstitutionFilter pat)
                (countReconstitution Reconstitution.filter pat)

        testList
            "the live sheets"
            [
                test "today a patient with no department matches every solution rule and every reconstitution" {
                    countSolution Original.filterPatient None
                    |> Expect.equal "every solution rule" solutionRows.Length

                    countReconstitution Original.reconstitutionFilter None
                    |> Expect.equal "every reconstitution" reconstitutions.Length
                }

                test "after: a patient with no department matches the rules that name none" {
                    countSolution PatientCategory.filterPatient None
                    |> Expect.equal "the solution rules naming none" (namingNone solutionDeps)

                    countReconstitution Reconstitution.filter None
                    |> Expect.equal "the reconstitutions naming none" (namingNone reconstitutionDeps)
                }

                test "after: a patient of ICK matches the rules of ICK and the rules that name none, as before" {
                    countSolution PatientCategory.filterPatient (Some "ICK")
                    |> Expect.equal "the solution rules of ICK or none" (naming (Some "ICK") solutionDeps)

                    countSolution PatientCategory.filterPatient (Some "ICK")
                    |> Expect.equal "the same as before" (countSolution Original.filterPatient (Some "ICK"))

                    countReconstitution Reconstitution.filter (Some "ICK")
                    |> Expect.equal "the same as before" (countReconstitution Original.reconstitutionFilter (Some "ICK"))
                }
            ]


runTestsWithCLIArgs [] [||] (testList "department" [ ruleTests; sheetTests ])
