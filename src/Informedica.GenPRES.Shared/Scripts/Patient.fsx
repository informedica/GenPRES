// What a patient setter keeps. Today every setter in `Shared/Models.fs` rebuilds the patient
// through `Patient.create`, and each one gets a part of that wrong: the age and gestational-age
// setters pass nothing for the weight and the height, so a measured weight typed before the age
// is dropped and the estimate shows in its place; `setWeight` and `setHeight` carry the other
// measure through `getWeight` and `getHeight`, which answer the estimate when nothing was
// measured, so an estimate is written back as a measured value. Both faults are silent.
//
// The one rule below covers both. A setter writes the field it is given and nothing else: every
// other measured value, the gender, the access devices, the renal function, the location and the
// department stay as they were, and the estimates are blanked, because they follow the age and
// the gender and the panel's next `applyNormalValues` is what fills them. What the setters did
// right is kept: a draft with no age gets one from its first age part, a part cleared reads as
// zero while another is set, a newborn on day zero stays a patient with an age, and a
// gestational age started from its days alone is a term one.
//
// Script-first draft (script-only policy) of the eight setters, → `Shared/Models.fs`, the
// `Patient` module; the tests → `tests/Informedica.GenPRES.Shared.Tests/ModelsTests.fs`.
//
// Run: `dotnet fsi Patient.fsx` from this directory.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "../Types.fs"
#load "../Calculations.fs"
#load "../Utils.fs"
#load "../Localization.fs"
#load "../Models.fs"

open Shared.Types
open Shared


module Patient =

    open Shared.Models.Patient


    /// The rule every setter follows: the draft, or the blank one, with the estimates blanked
    /// and one change applied. Nothing else on the patient is touched.
    let edit (change: Patient -> Patient) (p: Patient option) : Patient option =
        p |> Option.defaultValue empty |> withEstimates None None |> change |> Some


    /// One part of the age written from the field. A draft with no age gets one when the part
    /// is given and stays without one when it is not; a part cleared while the age exists
    /// reads as zero, so the age is never lost by emptying one field of it.
    let editAgePart (write: int -> Age -> Age) (s: string option) (p: Patient) =
        match p.Age, s |> Option.bind tryParse with
        | None, None -> p
        | age, v ->
            let age = age |> Option.defaultValue Age.ageZero |> write (v |> Option.defaultValue 0)
            { p with Age = Some age }


    /// One part of the gestational age written from the field, as the age parts are, with
    /// the term values, 37 weeks and 0 days, for a part that was never given.
    let editGestAgePart (write: int option -> GestAge -> GestAge) (s: string option) (p: Patient) =
        match p.GestationalAge, s |> Option.bind tryParse with
        | None, None -> p
        | ga, v ->
            let term: GestAge =
                {
                    Weeks = 37<week>
                    Days = 0<day>
                }

            { p with
                GestationalAge = ga |> Option.defaultValue term |> write v |> Some
            }


    let setYear s (p: Patient option) =
        p |> edit (editAgePart (fun v a -> { a with Years = v |> Measures.toYear }) s)


    let setMonth s (p: Patient option) =
        p |> edit (editAgePart (fun v a -> { a with Months = v |> Measures.toMonth }) s)


    let setWeek s (p: Patient option) =
        p |> edit (editAgePart (fun v a -> { a with Weeks = v |> Measures.toWeek }) s)


    let setDay s (p: Patient option) =
        p |> edit (editAgePart (fun v a -> { a with Days = v |> Measures.toDay }) s)


    let setGAWeek s (p: Patient option) =
        p
        |> edit (
            editGestAgePart
                (fun v ga ->
                    { ga with
                        Weeks = v |> Option.map Measures.toWeek |> Option.defaultValue 37<week>
                    }
                )
                s
        )


    let setGADay s (p: Patient option) =
        p
        |> edit (
            editGestAgePart
                (fun v ga ->
                    { ga with
                        Days = v |> Option.map Measures.toDay |> Option.defaultValue 0<day>
                    }
                )
                s
        )


    /// The measured weight in grams from the field; the height, measured or not, untouched.
    let setWeight s (p: Patient option) =
        p
        |> edit (fun p ->
            { p with
                Weight =
                    { p.Weight with
                        Measured = s |> Option.bind tryParse |> Option.map Measures.toGram
                    }
            }
        )


    /// The measured height in centimetres from the field; the weight, measured or not, untouched.
    let setHeight s (p: Patient option) =
        p
        |> edit (fun p ->
            { p with
                Height =
                    { p.Height with
                        Measured = s |> Option.bind tryParse |> Option.map Measures.toCm
                    }
            }
        )


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip

/// The original setters, to show the two faults they have and that the new ones keep what the
/// originals did right.
module Original = Shared.Models.Patient


/// A patient with everything set, measured and estimated, so that every field has something to
/// lose.
let full: Patient =
    {
        Age =
            Some
                {
                    Years = 2<year>
                    Months = 3<month>
                    Weeks = 1<week>
                    Days = 4<day>
                }
        GestationalAge =
            Some
                {
                    Weeks = 32<week>
                    Days = 5<day>
                }
        Weight =
            {
                EstimatedP3 = Some 10000<gram>
                Estimated = Some 12000<gram>
                EstimatedP97 = Some 14000<gram>
                Measured = Some 12500<gram>
            }
        Height =
            {
                EstimatedP3 = Some 85<cm>
                Estimated = Some 90<cm>
                EstimatedP97 = Some 95<cm>
                Measured = Some 91<cm>
            }
        Gender = Female
        Access = [ CVL; EnteralTube ]
        RenalFunction = Some(EGFR(Some 30, Some 50))
        Location = Some "bed 4"
        Department = Some "ICK"
    }


/// The same patient with the measures estimated only: what the panel holds after an age and a
/// gender were typed and the tables answered.
let estimated: Patient =
    { full with
        Weight = { full.Weight with Measured = None }
        Height = { full.Height with Measured = None }
    }


/// Every setter by name, applied to a value that fits its field.
let setters =
    [
        "setYear", Patient.setYear (Some "5")
        "setMonth", Patient.setMonth (Some "7")
        "setWeek", Patient.setWeek (Some "2")
        "setDay", Patient.setDay (Some "3")
        "setGAWeek", Patient.setGAWeek (Some "36")
        "setGADay", Patient.setGADay (Some "2")
        "setWeight", Patient.setWeight (Some "13000")
        "setHeight", Patient.setHeight (Some "95")
    ]


let ageSetters = setters |> List.take 4

let gestAgeSetters = setters |> List.skip 4 |> List.take 2


let get (p: Patient option) = p |> Option.defaultWith (fun () -> failtest "no patient")


let tests =
    testList
        "Patient setters keep what was measured"
        [
            testList
                "the faults of the setters as they are"
                [
                    test "an age typed after a weight drops the measured weight" {
                        Some full
                        |> Original.setYear (Some "5")
                        |> get
                        |> _.Weight.Measured
                        |> Expect.isNone "the original drops it"
                    }

                    test "a weight typed for an estimated height writes the estimate as measured" {
                        Some estimated
                        |> Original.setWeight (Some "13000")
                        |> get
                        |> _.Height.Measured
                        |> Expect.equal "the original promotes the estimate" (Some 90<cm>)
                    }
                ]

            testList
                "an age or gestational-age edit keeps the measured weight and height"
                [
                    for name, set in ageSetters @ gestAgeSetters do
                        test $"{name}" {
                            let p = Some full |> set |> get
                            p.Weight.Measured |> Expect.equal "the weight" full.Weight.Measured
                            p.Height.Measured |> Expect.equal "the height" full.Height.Measured
                        }
                ]

            testList
                "a weight or height edit keeps the age and the gestational age"
                [
                    for name, set in setters |> List.skip 6 do
                        test $"{name}" {
                            let p = Some full |> set |> get
                            p.Age |> Expect.equal "the age" full.Age
                            p.GestationalAge |> Expect.equal "the gestational age" full.GestationalAge
                        }
                ]

            testList
                "every setter keeps the gender, access, renal function, location and department"
                [
                    for name, set in setters do
                        test $"{name}" {
                            let p = Some full |> set |> get
                            p.Gender |> Expect.equal "the gender" full.Gender
                            p.Access |> Expect.equal "the access" full.Access
                            p.RenalFunction |> Expect.equal "the renal function" full.RenalFunction
                            p.Location |> Expect.equal "the location" full.Location
                            p.Department |> Expect.equal "the department" full.Department
                        }
                ]

            testList
                "no setter writes an estimate as measured"
                [
                    test "a weight typed for an estimated height leaves the height unmeasured" {
                        Some estimated
                        |> Patient.setWeight (Some "13000")
                        |> get
                        |> _.Height.Measured
                        |> Expect.isNone "not measured"
                    }

                    test "a height typed for an estimated weight leaves the weight unmeasured" {
                        Some estimated
                        |> Patient.setHeight (Some "95")
                        |> get
                        |> _.Weight.Measured
                        |> Expect.isNone "not measured"
                    }
                ]

            testList
                "the estimates are blank after every setter"
                [
                    for name, set in setters do
                        test $"{name}" {
                            let p = Some full |> set |> get
                            p.Weight.Estimated |> Expect.isNone "weight estimate"
                            p.Weight.EstimatedP3 |> Expect.isNone "weight p3"
                            p.Weight.EstimatedP97 |> Expect.isNone "weight p97"
                            p.Height.Estimated |> Expect.isNone "height estimate"
                            p.Height.EstimatedP3 |> Expect.isNone "height p3"
                            p.Height.EstimatedP97 |> Expect.isNone "height p97"
                        }
                ]

            testList
                "each setter writes its own field"
                [
                    test "setYear" {
                        Some full |> Patient.setYear (Some "5") |> get |> _.Age
                        |> Expect.equal "years" (Some { full.Age.Value with Years = 5<year> })
                    }

                    test "setMonth" {
                        Some full |> Patient.setMonth (Some "7") |> get |> _.Age
                        |> Expect.equal "months" (Some { full.Age.Value with Months = 7<month> })
                    }

                    test "setWeek" {
                        Some full |> Patient.setWeek (Some "2") |> get |> _.Age
                        |> Expect.equal "weeks" (Some { full.Age.Value with Weeks = 2<week> })
                    }

                    test "setDay" {
                        Some full |> Patient.setDay (Some "3") |> get |> _.Age
                        |> Expect.equal "days" (Some { full.Age.Value with Days = 3<day> })
                    }

                    test "setGAWeek" {
                        Some full |> Patient.setGAWeek (Some "36") |> get |> _.GestationalAge
                        |> Expect.equal "weeks" (Some { full.GestationalAge.Value with Weeks = 36<week> })
                    }

                    test "setGADay" {
                        Some full |> Patient.setGADay (Some "2") |> get |> _.GestationalAge
                        |> Expect.equal "days" (Some { full.GestationalAge.Value with Days = 2<day> })
                    }

                    test "setWeight" {
                        Some full |> Patient.setWeight (Some "13000") |> get |> _.Weight.Measured
                        |> Expect.equal "grams" (Some 13000<gram>)
                    }

                    test "setHeight" {
                        Some full |> Patient.setHeight (Some "95") |> get |> _.Height.Measured
                        |> Expect.equal "centimetres" (Some 95<cm>)
                    }
                ]

            testList
                "what the setters did right is kept, checked against the originals"
                [
                    testList
                        "a blank draft given one field"
                        [
                            for name, set in setters do
                                let original =
                                    match name with
                                    | "setYear" -> Original.setYear (Some "5")
                                    | "setMonth" -> Original.setMonth (Some "7")
                                    | "setWeek" -> Original.setWeek (Some "2")
                                    | "setDay" -> Original.setDay (Some "3")
                                    | "setGAWeek" -> Original.setGAWeek (Some "36")
                                    | "setGADay" -> Original.setGADay (Some "2")
                                    | "setWeight" -> Original.setWeight (Some "13000")
                                    | _ -> Original.setHeight (Some "95")

                                test $"{name}" {
                                    None |> set |> Expect.equal "as the original" (None |> original)
                                }
                        ]

                    test "a blank draft with a field cleared stays blank" {
                        None |> Patient.setYear None |> Expect.equal "no patient" (None |> Original.setYear None)
                        None |> Patient.setGADay None |> Expect.equal "no patient" (None |> Original.setGADay None)
                        None |> Patient.setWeight None |> Expect.equal "no patient" (None |> Original.setWeight None)
                    }

                    test "a newborn on day zero is a patient with an age" {
                        None
                        |> Patient.setDay (Some "0")
                        |> get
                        |> _.Age
                        |> Expect.equal "age zero" (Some Original.Age.ageZero)
                    }

                    test "clearing one age part keeps the age with that part zero" {
                        Some full
                        |> Patient.setMonth None
                        |> get
                        |> _.Age
                        |> Expect.equal "months zero" (Some { full.Age.Value with Months = 0<month> })
                    }

                    test "clearing the last set age part keeps an age of zero, as the original does" {
                        let p = None |> Patient.setYear (Some "5") |> Patient.setYear None
                        p |> get |> _.Age |> Expect.equal "age zero" (Some Original.Age.ageZero)

                        None
                        |> Original.setYear (Some "5")
                        |> Original.setYear None
                        |> get
                        |> _.Age
                        |> Expect.equal "the original does the same" (Some Original.Age.ageZero)
                    }

                    test "a gestational age started from its days alone is a term one" {
                        None
                        |> Patient.setGADay (Some "2")
                        |> get
                        |> _.GestationalAge
                        |> Expect.equal "37 weeks 2 days" (Some { Weeks = 37<week>; Days = 2<day> })
                    }

                    test "clearing the gestational weeks reads as term, as the original does" {
                        let expected: GestAge option = Some { Weeks = 37<week>; Days = 5<day> }
                        Some full |> Patient.setGAWeek None |> get |> _.GestationalAge
                        |> Expect.equal "term" expected

                        Some full |> Original.setGAWeek None |> get |> _.GestationalAge
                        |> Expect.equal "the original does the same" expected
                    }

                    test "a measure cleared is a measure gone, the other kept" {
                        let p = Some full |> Patient.setWeight None |> get
                        p.Weight.Measured |> Expect.isNone "weight gone"
                        p.Height.Measured |> Expect.equal "height kept" full.Height.Measured
                    }

                    test "a value that is not a number clears the field" {
                        Some full
                        |> Patient.setWeight (Some "twelve")
                        |> get
                        |> _.Weight.Measured
                        |> Expect.isNone "not a number"
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
