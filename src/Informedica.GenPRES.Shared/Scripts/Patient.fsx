// The patient record renamed to what it is on the wire and in the panel: `PatientDto`, every
// field optional, a draft or a reading. The old name stays as a type abbreviation, so that no
// caller changes in this step; the blank draft moves to `PatientDto.empty` with the old name
// aliased the same way. A rename only, no behaviour change.
//
// Script-first draft (script-only policy), → `Shared/Types.fs` and `Shared/Models.fs`. The
// types are drafted here under a suffix next to the loaded originals, and the tests pin what
// the abbreviation has to carry: equality, field-qualified copy-and-update, and pattern use
// under either name.
//
// Run: `dotnet fsi Patient.fsx` from this directory.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "../Types.fs"
#load "../Calculations.fs"
#load "../Utils.fs"
#load "../Localization.fs"
#load "../Models.fs"

open Expecto
open Expecto.Flip
open Shared.Types


/// → `Shared/Types.fs`: today's `Patient` record under the wire's name.
type PatientDto646 =
    {
        Age: Age option
        GestationalAge: GestAge option
        Weight: Weight
        Height: Height
        Gender: Gender
        Access: Access list
        RenalFunction: RenalFunction option
        Location: string option
        Department: string option
    }


/// → `Shared/Types.fs`, after the record group: the old name as an abbreviation.
type Patient646 = PatientDto646


/// → `Shared/Models.fs`: the blank draft under the wire's name.
module PatientDto646 =

    let empty: PatientDto646 =
        {
            Age = None
            GestationalAge = None
            Weight =
                {
                    EstimatedP3 = None
                    Estimated = None
                    EstimatedP97 = None
                    Measured = None
                }
            Height =
                {
                    EstimatedP3 = None
                    Estimated = None
                    EstimatedP97 = None
                    Measured = None
                }
            Gender = UnknownGender
            Access = []
            RenalFunction = None
            Location = None
            Department = None
        }


/// → `Shared/Models.fs`: the old name delegating, until its callers move.
module Patient646 =

    let empty = PatientDto646.empty


let tests =
    testList "patient dto rename" [
        test "the old name is the new record: one value, both names" {
            let asDto: PatientDto646 = Patient646.empty
            let asPatient: Patient646 = PatientDto646.empty

            asDto |> Expect.equal "the same value" asPatient
        }

        test "a copy-and-update field-qualified by the old name still resolves" {
            let p: Patient646 = Patient646.empty

            { p with
                Patient646.Weight.Measured = Some 32000<gram>
                Patient646.Gender = Female
            }
            |> fun q -> q.Weight.Measured, q.Gender
            |> Expect.equal "the fields set through the abbreviation" (Some 32000<gram>, Female)
        }

        test "a function typed on the old name takes a value built under the new" {
            let department (p: Patient646) = p.Department
            let dto: PatientDto646 = { PatientDto646.empty with Department = Some "ICK" }

            dto |> department |> Expect.equal "the department" (Some "ICK")
        }
    ]


runTestsWithCLIArgs [] [||] tests
