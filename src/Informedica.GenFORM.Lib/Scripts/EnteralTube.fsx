// Step 3.0 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #752): the
// access device gains an enteral tube. The contract model has had the case since the patient
// panel got its checkbox; the server's patient mapper drops it on the way in, so the domain
// never learns that a patient has a tube. With the case on the domain type the mapping of
// step 3.1 is total and the Dto reads it back.
//
// A union case cannot be added to the library from a script, so the union is shadowed here
// with the case, next to every function the compiler will point at once the case exists, and
// the tests pin that a patient's rules do not change: the access check has wildcard arms and
// needs no edit, and a rule's access is one of the venous ones or any, so a tube on the
// patient matches exactly the rules it matched before. Dosing, unchanged, pinned by the tests.
//
// What migration touches:
//
//   1. Types.fs: `EnteralTube` on `AccessDevice`, after `CVL`.
//   2. Patient.fs, module AccessDevice: the string form `enteral-tube` both ways.
//   3. Product.fs, module Location: `TUBE` both ways (a rule never carries it; the form is
//      there so the match is total and the string round-trips).
//   4. SolutionRule.fs, `printSolutionLimit`: the heading for a tube.
//   5. tests/Informedica.GenFORM.Tests/Tests.fs: the tests below, in PatientTests.
//
// The server's patient mapper keeps dropping the case until step 3.1 replaces it.
//
// Run from this directory: dotnet fsi EnteralTube.fsx

#I __SOURCE_DIRECTORY__

#r "nuget: Expecto"

#load "load.fsx"
// load.fsx stops at the libraries below this one; the built GenFORM is what is shadowed here
#r "../bin/Debug/net10.0/Informedica.GenForm.Lib.dll"
#r "nuget: MathNet.Numerics.FSharp"

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib


// ---------------------------------------------------------------------------
// 1. Types.fs: the union with the case
// ---------------------------------------------------------------------------

/// The types for Access.
type AccessDevice =
    // Peripheral Venous Access
    | PVL
    // Central Venous Access
    | CVL
    // An enteral feeding tube; no venous access
    | EnteralTube
    // Any Venous Access
    | AnyAccess


// The check as it is in Patient.fs, unchanged: a rule for any access takes every patient, a
// patient with no access recorded takes every rule, and otherwise the rule's access has to be
// among the patient's. A tube on the patient adds nothing a venous rule matches.
module VenousAccess =

    let check location venousAccess =
        match location, venousAccess with
        | AnyAccess, _ -> true
        | _, xs when xs |> List.isEmpty -> true
        | _ -> venousAccess |> List.exists ((=) location)


// ---------------------------------------------------------------------------
// 2. Patient.fs, module AccessDevice: the string form for the Dto
// ---------------------------------------------------------------------------

module AccessDevice =

    let toString =
        function
        | PVL -> "pvl"
        | CVL -> "cvl"
        | EnteralTube -> "enteral-tube"
        | AnyAccess -> "any"


    let tryFromString s =
        match s |> String.toLower |> String.trim with
        | "pvl" -> Some PVL
        | "cvl" -> Some CVL
        | "enteral-tube" -> Some EnteralTube
        | "any" -> Some AnyAccess
        | _ -> None


// ---------------------------------------------------------------------------
// 3. Product.fs, module Location: the rule-side string form
// ---------------------------------------------------------------------------

module Location =

    /// Get a string representation of the VenousAccess.
    let toString =
        function
        | PVL -> "PVL"
        | CVL -> "CVL"
        | EnteralTube -> "TUBE"
        | AnyAccess -> ""


    /// Get a VenousAccess from a string.
    let fromString s =
        match s with
        | _ when s |> String.equalsCapInsens "PVL" -> PVL
        | _ when s |> String.equalsCapInsens "CVL" -> CVL
        | _ when s |> String.equalsCapInsens "TUBE" -> EnteralTube
        | _ -> AnyAccess


// ---------------------------------------------------------------------------
// 4. SolutionRule.fs, printSolutionLimit: the heading per access
// ---------------------------------------------------------------------------

module SolutionRule =

    let accessHeading =
        function
        | CVL -> "###### centraal: \n* "
        | PVL -> "###### perifeer: \n* "
        | EnteralTube -> "###### sonde: \n* "
        | AnyAccess -> "* "


// ---------------------------------------------------------------------------
// 5. Tests, for tests/Informedica.GenFORM.Tests/Tests.fs, PatientTests
// ---------------------------------------------------------------------------

module EnteralTubeTests =

    open Expecto
    open Expecto.Flip

    module Lib = Informedica.GenForm.Lib.Types


    /// The check on the library's own cases, the rows a tube cannot touch.
    let libraryTable =
        [
            for rule in [ Lib.PVL; Lib.CVL; Lib.AnyAccess ] do
                for patient in [ []; [ Lib.PVL ]; [ Lib.CVL ]; [ Lib.PVL; Lib.CVL ] ] do
                    rule, patient, Informedica.GenForm.Lib.VenousAccess.check rule patient
        ]


    let shadow =
        function
        | Lib.PVL -> PVL
        | Lib.CVL -> CVL
        | Lib.AnyAccess -> AnyAccess


    let tests =
        testList
            "an enteral tube as access"
            [
                test "a patient's rules are unchanged: the check answers as before on every venous row" {
                    for rule, patient, before in libraryTable do
                        VenousAccess.check (shadow rule) (patient |> List.map shadow)
                        |> Expect.equal $"{rule} for {patient}" before
                }

                test "a tube on the patient matches what a venous access matched, and no venous rule by itself" {
                    VenousAccess.check PVL [ PVL; EnteralTube ] |> Expect.isTrue "a PVL rule, PVL and tube"
                    VenousAccess.check CVL [ PVL; EnteralTube ] |> Expect.isFalse "a CVL rule, PVL and tube"
                    VenousAccess.check PVL [ EnteralTube ] |> Expect.isFalse "a PVL rule, tube only"
                    VenousAccess.check CVL [ EnteralTube ] |> Expect.isFalse "a CVL rule, tube only"
                    VenousAccess.check AnyAccess [ EnteralTube ] |> Expect.isTrue "any access, tube only"
                }

                test "the string forms round-trip, the tube included" {
                    for a in [ PVL; CVL; EnteralTube; AnyAccess ] do
                        a
                        |> AccessDevice.toString
                        |> AccessDevice.tryFromString
                        |> Expect.equal $"{a} for the Dto" (Some a)

                        a |> Location.toString |> Location.fromString |> Expect.equal $"{a} for a rule" a

                    "tube" |> AccessDevice.tryFromString |> Expect.equal "not the Dto's word" None
                }

                test "a solution rule heading names the tube" {
                    SolutionRule.accessHeading EnteralTube |> Expect.equal "sonde" "###### sonde: \n* "
                    SolutionRule.accessHeading AnyAccess |> Expect.equal "as before" "* "
                }
            ]


EnteralTubeTests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||] |> ignore


// ---------------------------------------------------------------------------
// 6. Against the library: a PVL patient over three categories, the rows the migrated test
//    pins with the case in place. Run here on today's library.
// ---------------------------------------------------------------------------

let child =
    { Patient.patient with
        Department = Some "ICK"
        Gender = Male
        Age = Some(ValueUnit.singleWithUnit Units.Time.day 3650N)
        Weight = Some(ValueUnit.singleWithUnit Units.Weight.kiloGram 32N)
        Height = Some(ValueUnit.singleWithUnit Units.Height.centiMeter 140N)
        Access = [ Informedica.GenForm.Lib.Types.PVL ]
    }

let categories =
    [
        "pvl", { PatientCategory.empty with Access = Informedica.GenForm.Lib.Types.PVL }
        "cvl", { PatientCategory.empty with Access = Informedica.GenForm.Lib.Types.CVL }
        "any", { PatientCategory.empty with Access = Informedica.GenForm.Lib.Types.AnyAccess }
    ]

categories
|> List.map (fun (name, cat) -> name, PatientCategory.filterPatient child cat)
|> printfn "a PVL patient over the categories: %A"
