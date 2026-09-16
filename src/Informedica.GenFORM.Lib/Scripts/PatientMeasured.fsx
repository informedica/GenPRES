// Step 1.2a of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #733):
// the GenFORM Patient gains two flags, WeightMeasured and HeightMeasured. An estimated
// weight is a domain fact, possibly a caution, and the flags are what Patient.validate
// (step 1.2b) reads for the minimum data: an age, or a measured weight and a measured
// height. No rule reads them yet.
//
// Prototype per the script-only policy in AGENTS.md. A script cannot add a field to the
// compiled record, so the `Shadow` module below holds the record and the fixture as they
// will read after migration, and the tests run against that copy. What migration touches:
//
//   1. Types.fs, `type Patient`: the two fields below, after `Height`.
//   2. Patient.fs, `Patient.patient`: both flags set, so that every test patient built as
//      `{ Patient.patient with Weight = ... }` keeps counting as measured and no existing
//      test changes.
//   3. GenORDER Patient.fs, its own empty `patient`: the same two lines.
//   4. GenORDER Api.fs, the full Patient literal in the dose filter (around line 571):
//      `WeightMeasured = ctx.Patient.WeightMeasured` and the same for height, copied, not
//      decided there.
//   5. Nothing in the server: `Mappers.mapFromSharedPatient` copies `Patient.patient`, so
//      a mapped patient carries the fixture's flags until step 3.1 maps them from the
//      contract model.
//
// Run from this directory: dotnet fsi PatientMeasured.fsx

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


/// The Patient record and its fixture as they read after migration.
module Shadow =

    /// A patient in a clinical context: the data the dose rules are filtered on.
    /// The weight and the height each carry a flag saying whether the value held was
    /// measured; when the flag is clear the value is an estimate, from age or entered.
    /// With no value the flag says nothing.
    type Patient =
        {
            // The Location of the Patient
            Location: string option
            // The Department of the Patient
            Department: string option
            // A list of Diagnoses of the Patient
            Diagnoses: string[]
            // The Gender of the Patient
            Gender: Gender
            // The Age in days of the Patient
            Age: ValueUnit option
            // The Weight in grams of the Patient
            Weight: ValueUnit option
            // The Height in cm of the Patient
            Height: ValueUnit option
            // Whether the Weight held was measured; false for an estimate
            WeightMeasured: bool
            // Whether the Height held was measured; false for an estimate
            HeightMeasured: bool
            // The Gestational Age in days of the Patient
            GestAge: ValueUnit option
            // The Post Menstrual Age in days of the Patient
            PMAge: ValueUnit option
            // The administration access devices of the Patient
            Access: AccessDevice list
            // The Renal Function of the Patient
            RenalFunction: RenalFunction option
        }


    module Patient =

        /// An empty Patient. Both flags are set, so a weight or height given to a copy of
        /// it counts as measured unless the copy says otherwise.
        let patient =
            {
                Location = None
                Department = None
                Diagnoses = [||]
                Gender = AnyGender
                Age = None
                Weight = None
                Height = None
                WeightMeasured = true
                HeightMeasured = true
                GestAge = None
                PMAge = None
                Access = []
                RenalFunction = None
            }


// ---------------------------------------------------------------------------
// Tests, for PatientTests in tests/Informedica.GenFORM.Tests/Tests.fs.
// ---------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Shadow


let kg = Units.Weight.kiloGram
let cm = Units.Height.centiMeter


let tests =
    testList
        "Patient measured flags"
        [
            test "the empty patient has both flags set" {
                Patient.patient.WeightMeasured |> Expect.isTrue "weight counts as measured"
                Patient.patient.HeightMeasured |> Expect.isTrue "height counts as measured"
            }

            test "a test patient built as a copy keeps counting as measured" {
                // the shape every existing PatientTests case uses
                let pat =
                    { Patient.patient with
                        Weight = Some(ValueUnit.singleWithUnit kg 15N)
                        Height = Some(ValueUnit.singleWithUnit cm 100N)
                    }

                pat.WeightMeasured |> Expect.isTrue "the weight is measured"
                pat.HeightMeasured |> Expect.isTrue "the height is measured"
                pat.Weight |> Expect.isSome "the weight is held"
            }

            test "an estimate is the held value with its flag clear, and survives copies" {
                let estimated =
                    { Patient.patient with
                        Age = Some(ValueUnit.singleWithUnit Units.Time.year 10N)
                        Weight = Some(ValueUnit.singleWithUnit kg 32N)
                        WeightMeasured = false
                    }

                let copied = { estimated with Department = Some "ICK" }

                copied.WeightMeasured |> Expect.isFalse "still an estimate after a copy"
                copied.HeightMeasured |> Expect.isTrue "the other flag is untouched"
                copied.Weight |> Expect.equal "the estimate is the held value" estimated.Weight
            }

            test "the flags are independent" {
                let pat =
                    { Patient.patient with
                        Weight = Some(ValueUnit.singleWithUnit kg (35N / 10N))
                        Height = Some(ValueUnit.singleWithUnit cm 50N)
                        HeightMeasured = false
                    }

                (pat.WeightMeasured, pat.HeightMeasured)
                |> Expect.equal "measured weight, estimated height" (true, false)
            }
        ]


runTestsWithCLIArgs [] [| "--summary" |] tests
