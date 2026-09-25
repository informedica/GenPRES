/// A dose rule that says adults matches a patient of eighteen years and over, and no other,
/// a patient with no age included. Today the IsAdult facet answers an empty age range, which
/// the age filter reads as no restriction, so such a rule would match a newborn.
///
/// Prototype for the migration into GenFORM.Lib: the adult threshold as one ValueUnit in
/// Utils.ValueUnit, read from the domain's eighteen years in GenCORE.Lib; PatientCategory.getAge
/// answering that range for IsAdult, so the three matchers that read it match adults only;
/// PatientCategory.printAge printing the facet as "volwassenen", and toString using it. The TODO
/// above getAge goes with it, and with it the reason the extraction boundary is gated shut.
///
/// Run: cd src/Informedica.GenFORM.Lib/Scripts && dotnet fsi Adults.fsx

#I __SOURCE_DIRECTORY__

#load "load.fsx"
#r "../../Informedica.GenForm.Lib/bin/Debug/net10.0/Informedica.GenForm.Lib.dll"
#r "nuget: Expecto, 10.2.3"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

open System
open Expecto
open Expecto.Flip
open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Utils


// ── Utils.fs, the threshold ───────────────────────────────────────────────────────────────


module Utils =

    module ValueUnit =

        open Informedica.GenForm.Lib.Utils.ValueUnit
        open Informedica.GenCore.Lib.Patients

        /// The age from which a patient is an adult: the domain's eighteen years, stated once
        /// in GenCORE.Lib and read here as the lower bound of the age range an adult rule
        /// applies to.
        let ageAdult =
            AgeValue.eighteen
            |> AgeValue.SetGet.getYearsDef0
            |> int
            |> BigRational.fromInt
            |> ValueUnit.singleWithUnit Units.Time.year


// ── Patient.fs, the matcher and the printing ──────────────────────────────────────────────


module PatientCategory =

    open Informedica.GenForm.Lib.PatientCategory


    /// The age range a category applies to: the absolute range it names, or, for a rule that
    /// says adults, from the adult threshold up. A patient with no age is in neither, since
    /// the facet asserts something about the patient and nothing is known.
    let getAge (pat: PatientCategory) =
        match pat.Age with
        | AbsoluteAge a -> a
        | IsAdult ->
            { MinMax.empty with
                Min = Some(Inclusive Utils.ValueUnit.ageAdult)
            }


    /// The age as the category words it: "volwassenen" for a rule that says adults, the range
    /// otherwise.
    let printAge (pat: PatientCategory) =
        match pat.Age with
        | IsAdult -> "volwassenen"
        | AbsoluteAge a -> a |> printAgeMinMax


    /// The line of toString that changes: the age printed as the category words it.
    let ageLine (pat: PatientCategory) = pat |> printAge


    /// The age predicate of the three matchers, as PatientCategory.filter and filterPatient
    /// apply it: the patient's age within the category's range, none within an empty one only.
    let ageMatches (patientAge: ValueUnit option) (pat: PatientCategory) =
        patientAge |> MinMax.inRange (pat |> getAge)


// ── The proof ─────────────────────────────────────────────────────────────────────────────


let years n =
    n |> BigRational.fromInt |> ValueUnit.singleWithUnit Units.Time.year


let adults = { PatientCategory.empty with Age = IsAdult }


let children =
    { PatientCategory.empty with
        Age =
            { MinMax.empty with
                Min = Some(Inclusive(years 1))
                Max = Some(Exclusive(years 18))
            }
            |> AbsoluteAge
    }


let anyAge = PatientCategory.empty


let tests =
    testList
        "adults"
        [
            test "the threshold is the domain's eighteen years" {
                Utils.ValueUnit.ageAdult
                |> ValueUnit.convertTo Units.Time.year
                |> ValueUnit.getValue
                |> Expect.equal "eighteen" [| 18N |]
            }

            test "an adult rule's age range starts at the threshold and has no top" {
                let range = adults |> PatientCategory.getAge

                (range.Min, range.Max)
                |> Expect.equal "from eighteen, inclusive" (Some(Inclusive Utils.ValueUnit.ageAdult), None)
            }

            test "an absolute range is answered as it is" {
                children
                |> PatientCategory.getAge
                |> Expect.equal "the children's range" (
                    match children.Age with
                    | AbsoluteAge a -> a
                    | IsAdult -> MinMax.empty
                )
            }

            test "an adult rule matches eighteen and over" {
                [ 18; 40; 90 ]
                |> List.map (fun y -> adults |> PatientCategory.ageMatches (Some(years y)))
                |> Expect.allEqual "all adults" true
            }

            test "an adult rule matches no child" {
                [ 0; 1; 10; 17 ]
                |> List.map (fun y -> adults |> PatientCategory.ageMatches (Some(years y)))
                |> Expect.allEqual "no child" false
            }

            test "an adult rule does not match a patient with no age" {
                adults |> PatientCategory.ageMatches None |> Expect.isFalse "unknown is not an adult"
            }

            test "a rule with no age bound still matches everyone, a patient with no age included" {
                [ Some(years 0); Some(years 17); Some(years 40); None ]
                |> List.map (fun a -> anyAge |> PatientCategory.ageMatches a)
                |> Expect.allEqual "everyone" true
            }

            test "a children's rule still matches its range and nothing else" {
                (children |> PatientCategory.ageMatches (Some(years 10)),
                 children |> PatientCategory.ageMatches (Some(years 18)),
                 children |> PatientCategory.ageMatches None)
                |> Expect.equal "ten yes, eighteen no, unknown no" (true, false, false)
            }

            test "an adult rule and a children's rule never both match one patient" {
                [ 0; 5; 17; 18; 30 ]
                |> List.map (fun y ->
                    let a = Some(years y)
                    adults |> PatientCategory.ageMatches a && children |> PatientCategory.ageMatches a
                )
                |> Expect.allEqual "never both" false
            }

            test "the facet prints as adults, an absolute range as before" {
                adults |> PatientCategory.printAge |> Expect.equal "volwassenen" "volwassenen"

                children
                |> PatientCategory.printAge
                |> Expect.equal "the range, as today" (PatientCategory.printAgeMinMax (children |> PatientCategory.getAge))

                anyAge |> PatientCategory.printAge |> Expect.equal "nothing" ""
            }

            test "the live sheet has no IsAdult column, so no rule carries the facet today" {
                match Environment.GetEnvironmentVariable "GENPRES_URL_ID" with
                | null
                | "" -> skiptest "GENPRES_URL_ID is not set"
                | urlId ->
                    match Informedica.Utils.Lib.Web.GoogleSheets.getCsvDataFromSheetSync urlId "DoseRules" with
                    | Error e -> failtest e
                    | Ok rows -> rows[0] |> Array.contains "IsAdult" |> Expect.isFalse "no such column"
            }
        ]


runTestsWithCLIArgs [] [||] tests
