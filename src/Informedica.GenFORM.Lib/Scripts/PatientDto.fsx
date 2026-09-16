// Step 1.2b of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #733):
// Patient.validate, the minimum-data rule the server's patient gate applies today
// (Shared.Models.Patient.validate, read by ServerApi.Patient), moved to the domain and
// reading the measured flags; and Patient.Dto, the serializable shape of a GenFORM Patient,
// whose fromDto calls validate. ADR-0008 invariants 1 to 4: one aggregate, toDto total,
// fromDto a Result that never throws, a Dto only at the boundary.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. Types.fs: `PatientError`, next to `Patient`.
//   2. Patient.fs: `Gender.tryFromString` in module Gender; the modules `AccessDevice` and
//      `RenalFunction` (string forms) before module Patient; `Patient.validate` and the
//      nested `Patient.Dto` module at the end of module Patient.
//   3. tests/Informedica.GenFORM.Tests/Tests.fs: the tests below in PatientTests.
//
// Canonical units: age, gestational age and post-menstrual age in days, weight in kilograms,
// height in centimetres. `toDto` converts to them, so law L1 (`fromDto (toDto x) = Ok x`)
// holds for a valid patient held in those units, which is what the server's mapper builds.
//
// Run from this directory: dotnet fsi PatientDto.fsx

#I __SOURCE_DIRECTORY__

#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: Expecto"

#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net10.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net10.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.GenForm.Lib/bin/Debug/net10.0/Informedica.GenForm.Lib.dll"

// after MathNet, so that the `N` literal is the Utils BigRational the units library takes
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib


// ---------------------------------------------------------------------------
// 1. Types.fs
// ---------------------------------------------------------------------------

/// Why a value is no patient, or why a Dto does not parse to one.
[<RequireQualifiedAccess>]
type PatientError =
    // Below the minimum data: no age, and no measured weight with a measured height
    | NoAgeOrMeasuredWeightAndHeight
    // A gender string the Dto carries that names no gender
    | UnknownGender of string
    // An access string the Dto carries that names no access device
    | UnknownAccess of string
    // A renal function string the Dto carries that names no renal function
    | UnknownRenalFunction of string


// ---------------------------------------------------------------------------
// 2. Patient.fs
// ---------------------------------------------------------------------------

/// For module Gender: the strict reading a Dto needs, next to `fromString`, which
/// reads anything unknown as AnyGender.
module Gender =

    // the original module's functions stay reachable through this name
    open Informedica.GenForm.Lib.Gender


    /// A gender from its string form; an empty string is AnyGender, anything else
    /// that `toString` never writes is None.
    let tryFromString s =
        match s |> String.toLower |> String.trim with
        | "man" -> Some Male
        | "vrouw" -> Some Female
        | "" -> Some AnyGender
        | _ -> None


/// The string form of an access device, for the Dto.
module AccessDevice =

    let toString =
        function
        | PVL -> "pvl"
        | CVL -> "cvl"
        | AnyAccess -> "any"


    let tryFromString s =
        match s |> String.toLower |> String.trim with
        | "pvl" -> Some PVL
        | "cvl" -> Some CVL
        | "any" -> Some AnyAccess
        | _ -> None


/// The string form of a renal function, for the Dto: `egfr:min:max` with an empty
/// bound for None, or the name of a dialysis.
module RenalFunction =

    let toString =
        function
        | EGFR(min, max) ->
            let bound = Option.map string >> Option.defaultValue ""
            $"egfr:{bound min}:{bound max}"
        | IntermittentHemodialysis -> "intermittent-hemodialysis"
        | ContinuousHemodialysis -> "continuous-hemodialysis"
        | PeritonealDialysis -> "peritoneal-dialysis"


    let tryFromString (s: string) =
        let bound (b: string) =
            match b.Trim() with
            | "" -> Some None
            | b ->
                match System.Int32.TryParse b with
                | true, n -> Some(Some n)
                | _ -> None

        match s |> String.toLower |> String.trim with
        | "intermittent-hemodialysis" -> Some IntermittentHemodialysis
        | "continuous-hemodialysis" -> Some ContinuousHemodialysis
        | "peritoneal-dialysis" -> Some PeritonealDialysis
        | s when s.StartsWith "egfr:" ->
            match s.Split ':' with
            | [| _; min; max |] ->
                match bound min, bound max with
                | Some min, Some max -> Some(EGFR(min, max))
                | _ -> None
            | _ -> None
        | _ -> None


/// For the end of module Patient.
module Patient =

    open Informedica.GenForm.Lib.Patient


    /// The patient the value is, or why it is none: the minimum data is an age, or a
    /// measured weight with a measured height. Below that there is no patient, no
    /// order context and no evaluation; a missing datum never matches a bounded range.
    let validate (pat: Patient) : Result<Patient, PatientError> =
        let measured (v: ValueUnit option) flag = v.IsSome && flag

        if
            pat.Age.IsSome
            || (measured pat.Weight pat.WeightMeasured
                && measured pat.Height pat.HeightMeasured)
        then
            Ok pat
        else
            Error PatientError.NoAgeOrMeasuredWeightAndHeight


    /// The serializable shape of a Patient: primitives only, one aggregate, no logic.
    /// Ages in days, the weight in kilograms, the height in centimetres; gender, access
    /// and renal function as the strings their modules write.
    module Dto =

        type Dto =
            {
                Location: string option
                Department: string option
                Diagnoses: string[]
                Gender: string
                AgeDays: BigRational option
                WeightKg: BigRational option
                HeightCm: BigRational option
                WeightMeasured: bool
                HeightMeasured: bool
                GestAgeDays: BigRational option
                PMAgeDays: BigRational option
                Access: string[]
                RenalFunction: string option
            }


        let private value unit (vu: ValueUnit) =
            vu |> ValueUnit.convertTo unit |> ValueUnit.getValue |> Array.tryExactlyOne


        let private withUnit unit (br: BigRational) = ValueUnit.singleWithUnit unit br


        let private toResult err =
            function
            | Some v -> Ok v
            | None -> Error err


        /// Total: every patient has a Dto.
        let toDto (pat: Patient) : Dto =
            {
                Location = pat.Location
                Department = pat.Department
                Diagnoses = pat.Diagnoses
                Gender = pat.Gender |> Gender.toString
                AgeDays = pat.Age |> Option.bind (value Units.Time.day)
                WeightKg = pat.Weight |> Option.bind (value Units.Weight.kiloGram)
                HeightCm = pat.Height |> Option.bind (value Units.Height.centiMeter)
                WeightMeasured = pat.WeightMeasured
                HeightMeasured = pat.HeightMeasured
                GestAgeDays = pat.GestAge |> Option.bind (value Units.Time.day)
                PMAgeDays = pat.PMAge |> Option.bind (value Units.Time.day)
                Access = pat.Access |> List.map AccessDevice.toString |> List.toArray
                RenalFunction = pat.RenalFunction |> Option.map RenalFunction.toString
            }


        /// The patient a Dto is, or every reason it is none: a string that names nothing,
        /// and a value below the minimum data. Never throws, never normalizes.
        let fromDto (dto: Dto) : Result<Patient, PatientError list> =
            let gender =
                dto.Gender
                |> Gender.tryFromString
                |> toResult (PatientError.UnknownGender dto.Gender)

            let access =
                dto.Access
                |> Array.toList
                |> List.map (fun s ->
                    s
                    |> AccessDevice.tryFromString
                    |> toResult (PatientError.UnknownAccess s)
                )

            let renal =
                match dto.RenalFunction with
                | None -> Ok None
                | Some s ->
                    s
                    |> RenalFunction.tryFromString
                    |> Option.map Some
                    |> toResult (PatientError.UnknownRenalFunction s)

            let errors =
                [
                    match gender with
                    | Error e -> yield e
                    | Ok _ -> ()
                    for a in access do
                        match a with
                        | Error e -> yield e
                        | Ok _ -> ()
                    match renal with
                    | Error e -> yield e
                    | Ok _ -> ()
                ]

            match errors, gender, renal with
            | [], Ok gender, Ok renal ->
                {
                    Location = dto.Location
                    Department = dto.Department
                    Diagnoses = dto.Diagnoses
                    Gender = gender
                    Age = dto.AgeDays |> Option.map (withUnit Units.Time.day)
                    Weight = dto.WeightKg |> Option.map (withUnit Units.Weight.kiloGram)
                    Height = dto.HeightCm |> Option.map (withUnit Units.Height.centiMeter)
                    WeightMeasured = dto.WeightMeasured
                    HeightMeasured = dto.HeightMeasured
                    GestAge = dto.GestAgeDays |> Option.map (withUnit Units.Time.day)
                    PMAge = dto.PMAgeDays |> Option.map (withUnit Units.Time.day)
                    Access = access |> List.choose Result.toOption
                    RenalFunction = renal
                }
                |> validate
                |> Result.mapError List.singleton
            | errors, _, _ -> Error errors


// ---------------------------------------------------------------------------
// 3. Tests, for PatientTests.
// ---------------------------------------------------------------------------

open Expecto
open Expecto.Flip


module Fixtures =

    let kg = Units.Weight.kiloGram
    let cm = Units.Height.centiMeter
    let day = Units.Time.day

    /// A ten-year-old, weight and height measured, on a PVL.
    let child =
        { Informedica.GenForm.Lib.Patient.patient with
            Department = Some "ICK"
            Gender = Male
            Age = Some(ValueUnit.singleWithUnit day 3650N)
            Weight = Some(ValueUnit.singleWithUnit kg 32N)
            Height = Some(ValueUnit.singleWithUnit cm 140N)
            Access = [ PVL ]
        }

    /// A premature of two days, 30 weeks gestation, 1.2 kg.
    let premature =
        { Informedica.GenForm.Lib.Patient.patient with
            Gender = Female
            Age = Some(ValueUnit.singleWithUnit day 2N)
            GestAge = Some(ValueUnit.singleWithUnit day 210N)
            PMAge = Some(ValueUnit.singleWithUnit day 212N)
            Weight = Some(ValueUnit.singleWithUnit kg (12N / 10N))
            Height = Some(ValueUnit.singleWithUnit cm 38N)
            Access = [ CVL ]
            RenalFunction = Some(EGFR(Some 30, Some 50))
        }

    /// No age, but a measured weight and height.
    let measuredOnly =
        { Informedica.GenForm.Lib.Patient.patient with
            Weight = Some(ValueUnit.singleWithUnit kg 70N)
            Height = Some(ValueUnit.singleWithUnit cm 175N)
        }


let tests =
    testList
        "Patient validate and Dto"
        [
            testList
                "validate"
                [
                    test "an age alone is a patient" {
                        { Informedica.GenForm.Lib.Patient.patient with
                            Age = Some(ValueUnit.singleWithUnit Fixtures.day 3650N)
                        }
                        |> Patient.validate
                        |> Result.isOk
                        |> Expect.isTrue "a patient"
                    }

                    test "a measured weight and height without an age is a patient" {
                        Fixtures.measuredOnly
                        |> Patient.validate
                        |> Result.isOk
                        |> Expect.isTrue "a patient"
                    }

                    test "an estimated weight without an age is no patient" {
                        { Fixtures.measuredOnly with WeightMeasured = false }
                        |> Patient.validate
                        |> Expect.equal "below the minimum data" (Error PatientError.NoAgeOrMeasuredWeightAndHeight)
                    }

                    test "the empty patient is no patient" {
                        Informedica.GenForm.Lib.Patient.patient
                        |> Patient.validate
                        |> Expect.equal "below the minimum data" (Error PatientError.NoAgeOrMeasuredWeightAndHeight)
                    }
                ]

            testList
                "L1, fromDto (toDto x) = Ok x"
                [
                    for name, pat in
                        [
                            "a child", Fixtures.child
                            "a premature", Fixtures.premature
                            "measured only", Fixtures.measuredOnly
                        ] do
                        test $"{name} round-trips" {
                            pat
                            |> Patient.Dto.toDto
                            |> Patient.Dto.fromDto
                            |> Expect.equal "the same patient" (Ok pat)
                        }
                ]

            testList
                "L2, fromDto d |> Result.map toDto = Ok d"
                [
                    for name, pat in [ "a child", Fixtures.child; "a premature", Fixtures.premature ] do
                        test $"{name}'s Dto round-trips" {
                            let dto = pat |> Patient.Dto.toDto

                            dto
                            |> Patient.Dto.fromDto
                            |> Result.map Patient.Dto.toDto
                            |> Expect.equal "the same Dto" (Ok dto)
                        }
                ]

            testList
                "fromDto refuses"
                [
                    test "an unknown gender string" {
                        { (Fixtures.child |> Patient.Dto.toDto) with Gender = "x" }
                        |> Patient.Dto.fromDto
                        |> Expect.equal "named" (Error [ PatientError.UnknownGender "x" ])
                    }

                    test "an unknown access string, and every error is reported" {
                        { (Fixtures.child |> Patient.Dto.toDto) with
                            Gender = "x"
                            Access = [| "pvl"; "tube" |]
                            RenalFunction = Some "egfr:a:b"
                        }
                        |> Patient.Dto.fromDto
                        |> Expect.equal
                            "all three"
                            (Error
                                [
                                    PatientError.UnknownGender "x"
                                    PatientError.UnknownAccess "tube"
                                    PatientError.UnknownRenalFunction "egfr:a:b"
                                ])
                    }

                    test "a draft with no age and no measured weight and height" {
                        { (Fixtures.child |> Patient.Dto.toDto) with
                            AgeDays = None
                            WeightMeasured = false
                        }
                        |> Patient.Dto.fromDto
                        |> Expect.equal "no patient" (Error [ PatientError.NoAgeOrMeasuredWeightAndHeight ])
                    }
                ]

            testList
                "string forms"
                [
                    test "renal function strings round-trip" {
                        [
                            EGFR(Some 30, Some 50)
                            EGFR(None, Some 10)
                            EGFR(Some 50, None)
                            IntermittentHemodialysis
                            ContinuousHemodialysis
                            PeritonealDialysis
                        ]
                        |> List.map (RenalFunction.toString >> RenalFunction.tryFromString)
                        |> Expect.equal
                            "each is read back"
                            (
                                [
                                    EGFR(Some 30, Some 50)
                                    EGFR(None, Some 10)
                                    EGFR(Some 50, None)
                                    IntermittentHemodialysis
                                    ContinuousHemodialysis
                                    PeritonealDialysis
                                ]
                                |> List.map Some
                            )
                    }

                    test "toDto converts to the canonical units" {
                        let dto =
                            { Fixtures.child with
                                Weight = Some(ValueUnit.singleWithUnit Units.Weight.gram 32000N)
                                Age = Some(ValueUnit.singleWithUnit Units.Time.week 10N)
                            }
                            |> Patient.Dto.toDto

                        dto.WeightKg |> Expect.equal "kilograms" (Some 32N)
                        dto.AgeDays |> Expect.equal "days" (Some 70N)
                    }
                ]
        ]


runTestsWithCLIArgs [] [| "--summary" |] tests
