// The department default, from one source. A patient is prescribed for in a department, and on
// the live sheets nine in ten solution rules and reconstitutions name one, so a patient without
// a department, which is every web patient whose url carries no `dp`, needs one before the
// rules are matched. Today the `ICK` literal is written in three places, none of them the one
// that matters: the formulary path's mapper, the MCP host's `buildPatient`, and nowhere on the
// order-context path at all, where the contract patient enters the domain as it is and
// `OrderContext.getRules` matches with whatever department it carries.
//
// The default cannot be written on the patient at the boundary: the contract patient the
// server answers with must equal the one it was sent, or the signed plan reads as changed.
// So the default is applied where rules are matched and nowhere else, and it comes from one
// value the server holds: `Departments`, the names the loaded rules carry and the default
// among them, a resource in the registry derived from the solution-rule rows and the
// reconstitutions. Everything that needs a department reads it there: `getRules`, the MCP
// host, the formulary mapper, and the contract, so the panel can show which department is in
// force and that it is the default.
//
// Script-first draft (script-only policy): the type → `GenFORM.Lib/Types.fs`; the module and
// the key → `GenFORM.Lib/Resources.fs`; the readers in step 6b: `GenORDER.Lib/Api.fs` `getRules`
// (the filter's department `forPatient`), `McpTools.GenOrder.fs` `buildPatient`,
// `ServerApi.Services.fs` and the contract's `ServerSettings`. The tests →
// `tests/Informedica.GenFORM.Tests/Tests.fs`.
//
// Run: `dotnet fsi Departments.fsx` from this directory. The last test list reads the live
// sheets through `GENPRES_URL_ID` from `.env`, and is skipped when that is not set.

#load "load.fsx"
#r "../../Informedica.GenForm.Lib/bin/Debug/net10.0/Informedica.GenForm.Lib.dll"

open System

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore

open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources


/// The departments a patient can be prescribed for: the names the loaded rules carry, and the
/// one a patient without a department is prescribed for. → `Types.fs`.
type Departments =
    {
        /// Every department a solution rule or a reconstitution names, the default included,
        /// distinct and sorted.
        Names: string[]
        /// The department a patient without one is prescribed for.
        Default: string
    }


module Departments =

    /// One hospital's ward written in code, and the only thing that keeps a patient without a
    /// department from losing every ward's solution rules and reconstitutions. It stays until
    /// the department is the user's to choose; then it becomes configuration, and this is the
    /// one place that changes.
    let defaultDepartment = "ICK"


    /// The departments named by the rules, blanks ignored, the default among them whether or
    /// not a rule names it.
    let ofNamed (named: string option seq) : Departments =
        {
            Names =
                named
                |> Seq.choose id
                |> Seq.map _.Trim()
                |> Seq.filter (String.isNullOrWhiteSpace >> not)
                |> Seq.append [ defaultDepartment ]
                |> Seq.distinct
                |> Seq.sort
                |> Seq.toArray
            Default = defaultDepartment
        }


    /// From the loaded rows: the solution-rule rows and the reconstitutions, the two sheets
    /// that name departments.
    let ofRules (solutionRows: SolutionRuleData[]) (reconstitutions: Reconstitution[]) =
        Seq.append (solutionRows |> Seq.map _.Department) (reconstitutions |> Seq.map _.Department)
        |> ofNamed


    /// The department a patient is prescribed for: the one given, or the default. Applied
    /// where rules are matched, never written on the patient.
    let forPatient (departments: Departments) (department: string option) =
        department |> Option.orElse (Some departments.Default)


    /// Whether a department is one the rules know, for an input to be checked against.
    let isKnown (departments: Departments) (department: string) =
        departments.Names |> Array.contains department


module Keys =

    /// → `Resources.Keys`.
    let departments = ResourceKey.create<Departments> "departments"


/// → the `defaultRegistry` entry: derived, so it loads once and after the two it reads.
let departmentsLoader: ResourceLoader =
    derive (fun r -> Departments.ofRules (r.Get Keys.solutionRuleData) (r.Get Keys.reconstitution))


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


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


let ruleTests =
    testList
        "the departments"
        [
            test "the names are what the rules name, distinct and sorted, blanks ignored" {
                [ Some "NEO"; Some "ICC"; None; Some "NEO"; Some " "; Some "ICC" ]
                |> Departments.ofNamed
                |> _.Names
                |> Expect.equal "ICC, ICK, NEO" [| "ICC"; "ICK"; "NEO" |]
            }

            test "the default is among the names whether or not a rule names it" {
                [] |> Departments.ofNamed |> _.Names |> Expect.equal "ICK alone" [| "ICK" |]

                [ Some "ICK" ]
                |> Departments.ofNamed
                |> _.Names
                |> Expect.equal "ICK once" [| "ICK" |]
            }

            test "the default is the one literal" {
                ([] |> Departments.ofNamed).Default |> Expect.equal "ICK" Departments.defaultDepartment
            }

            test "a patient without a department is prescribed for in the default, one with keeps it" {
                let deps = Departments.ofNamed [ Some "NEO" ]
                None |> Departments.forPatient deps |> Expect.equal "the default" (Some "ICK")
                Some "NEO" |> Departments.forPatient deps |> Expect.equal "kept" (Some "NEO")
                Some "OTHER" |> Departments.forPatient deps |> Expect.equal "kept, known or not" (Some "OTHER")
            }

            test "a department is known when a rule names it or it is the default" {
                let deps = Departments.ofNamed [ Some "NEO" ]
                "NEO" |> Departments.isKnown deps |> Expect.isTrue "NEO"
                "ICK" |> Departments.isKnown deps |> Expect.isTrue "ICK"
                "ICU" |> Departments.isKnown deps |> Expect.isFalse "ICU"
            }

            test "the registry derives it from the solution-rule rows and the reconstitutions, once" {
                let mutable loads = 0

                let registry =
                    Map
                        [
                            Keys.solutionRuleData.Name, ofResult (fun () -> Ok([||]: SolutionRuleData[]))
                            Keys.reconstitution.Name,
                            ofResult (fun () ->
                                loads <- loads + 1
                                Ok [| reconstitutionOf (Some "NEO"); reconstitutionOf None; reconstitutionOf (Some "ICC") |]
                            )
                            Keys.departments.Name, departmentsLoader
                        ]

                let engine = LoadEngine registry
                let deps = engine.Resolve Keys.departments
                engine.Resolve Keys.departments |> ignore

                deps.Names |> Expect.equal "ICC, ICK, NEO" [| "ICC"; "ICK"; "NEO" |]
                loads |> Expect.equal "the reconstitutions loaded once" 1
            }
        ]


let dataUrlId = Environment.GetEnvironmentVariable "GENPRES_URL_ID"


let sheetTests =
    if dataUrlId |> String.isNullOrWhiteSpace then
        testList "the live sheets" [ test "skipped: GENPRES_URL_ID is not set" { () } ]
    else
        let registry =
            defaultRegistry FormLogging.noOp dataUrlId |> Map.add Keys.departments.Name departmentsLoader

        let engine = LoadEngine registry
        let deps = engine.Resolve Keys.departments

        printfn "Departments on the live sheets: %s, default %s" (deps.Names |> String.concat ", ") deps.Default

        testList
            "the live sheets"
            [
                test "the names are the departments the two sheets name, and the default" {
                    let named =
                        Seq.append
                            (engine.Resolve Keys.solutionRuleData |> Seq.map _.Department)
                            (engine.Resolve Keys.reconstitution |> Seq.map _.Department)
                        |> Seq.choose id
                        |> Seq.append [ Departments.defaultDepartment ]
                        |> Seq.distinct
                        |> Seq.sort
                        |> Seq.toArray

                    deps.Names |> Expect.equal "the same set" named
                    deps.Names |> Array.contains "ICK" |> Expect.isTrue "ICK among them"
                }
            ]


runTestsWithCLIArgs [] [||] (testList "departments" [ ruleTests; sheetTests ])
