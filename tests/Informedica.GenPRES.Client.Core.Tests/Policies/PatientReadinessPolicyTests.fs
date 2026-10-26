/// What the pages can do with the patient draft, and what they say when it is not enough.
module Informedica.GenPRES.Client.Core.Tests.Policies.PatientReadinessPolicyTests

open Informedica.GenPRES.Client.Core.Models
open Informedica.GenPRES.Client.Core.Policies
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types
open PatientReadinessPolicy


/// A translator that shows which term was asked for, so a test can assert terms, not prose.
let named (term: Terms) = $"<{term}>"


let empty = Models.Patient.empty


/// An age of five years, nothing else.
let aged = PatientEdit.setYear (Some "5") None


/// An age of ten weeks, nothing else: younger than 28 weeks, so the gestational age matters.
let infant = PatientEdit.setWeek (Some "10") None


let withWeight (p: Patient option) = p |> PatientEdit.setWeight (Some "18000")


let withHeight (p: Patient option) = p |> PatientEdit.setHeight (Some "110")


let withGestationalAge (p: Patient option) = p |> PatientEdit.setGAWeek (Some "36")


/// The weight and the height estimated, as the App fills them in from the age. Applied last,
/// since a setter blanks the estimates as the panel's edit does.
let estimated (p: Patient option) =
    p
    |> Option.map (fun p ->
        { p with
            Weight = { p.Weight with Estimated = Some 18000<gram> }
            Height = { p.Height with Estimated = Some 110<cm> }
        }
    )


let needs = [ Needs.Calculation; Needs.DoseCheck; Needs.PlanMedication ]


[<Tests>]
let tests =
    testList
        "the patient readiness"
        [
            testList
                "readiness"
                [
                    test "no draft is no data" { None |> readiness |> Expect.equal "nothing entered" Readiness.NoData }

                    test "an empty draft is no data" {
                        Some empty |> readiness |> Expect.equal "nothing entered" Readiness.NoData
                    }

                    test "a draft with only a gender, a department or a renal function is no patient" {
                        for draft in
                            [
                                PatientEdit.setGender "male" None
                                PatientDraftPolicy.setDepartment (Some "ICK") None
                                Some { empty with RenalFunction = Some Types.RenalFunction.PeritonealDialysis }
                            ] do
                            draft |> readiness |> Expect.equal $"%A{draft}" Readiness.NoPatient
                    }

                    test "a draft with a weight only is no patient" {
                        None
                        |> withWeight
                        |> readiness
                        |> Expect.equal "no age, no height" Readiness.NoPatient
                    }

                    test "a draft with a weight and a height and no age is a patient missing the age" {
                        None
                        |> withWeight
                        |> withHeight
                        |> readiness
                        |> Expect.equal "a patient by its measurements" (Readiness.Patient [ Missing.Age ])
                    }

                    test "an age alone misses the weight and the height" {
                        aged
                        |> readiness
                        |> Expect.equal
                            "nothing to estimate from yet"
                            (Readiness.Patient [ Missing.Weight; Missing.Height ])
                    }

                    test "an age with a measured height misses the weight" {
                        aged
                        |> withHeight
                        |> readiness
                        |> Expect.equal "the weight" (Readiness.Patient [ Missing.Weight ])
                    }

                    test "an age with a measured weight misses the height" {
                        aged
                        |> withWeight
                        |> readiness
                        |> Expect.equal "the height" (Readiness.Patient [ Missing.Height ])
                    }

                    test "an age with both estimated is complete" {
                        aged
                        |> estimated
                        |> readiness
                        |> Expect.equal "estimates count" (Readiness.Patient [])
                    }

                    test "an age with both measured is complete" {
                        aged
                        |> withWeight
                        |> withHeight
                        |> readiness
                        |> Expect.equal "measured" (Readiness.Patient [])
                    }

                    test "a cleared weight over an estimate misses the weight and the height again" {
                        // the setter blanks the estimates, as the panel's edit does
                        aged
                        |> estimated
                        |> PatientEdit.setWeight None
                        |> readiness
                        |> Expect.equal "no estimate stands in" (Readiness.Patient [ Missing.Weight; Missing.Height ])
                    }

                    test "a patient younger than 28 weeks misses the gestational age" {
                        infant
                        |> estimated
                        |> readiness
                        |> Expect.equal "ten weeks, no gestational age" (Readiness.Patient [ Missing.GestationalAge ])
                    }

                    test "a patient younger than 28 weeks with a gestational age is complete" {
                        infant
                        |> withGestationalAge
                        |> estimated
                        |> readiness
                        |> Expect.equal "complete" (Readiness.Patient [])
                    }

                    test "a patient of 28 weeks or older needs no gestational age" {
                        for draft in [ PatientEdit.setWeek (Some "28") None; aged ] do
                            draft
                            |> estimated
                            |> readiness
                            |> Expect.equal $"%A{draft |> Option.map _.Age}" (Readiness.Patient [])
                    }

                    test "what is missing is said in one order: age, weight, height, gestational age" {
                        infant
                        |> readiness
                        |> Expect.equal
                            "weight, height, then gestational age"
                            (Readiness.Patient [ Missing.Weight; Missing.Height; Missing.GestationalAge ])
                    }
                ]

            testList
                "missing"
                [
                    test "no data asks for the data" {
                        Readiness.NoData
                        |> missing
                        |> Expect.equal "the data" (Some Terms.``Patient enter patient data``)
                    }

                    test "no patient asks for an age, or a weight and a height" {
                        Readiness.NoPatient
                        |> missing
                        |> Expect.equal "the minimum" (Some Terms.``Patient enter age or weight and height``)
                    }

                    test "a patient misses nothing to be one, whatever its calculations miss" {
                        for m in
                            [
                                []
                                [ Missing.Age ]
                                [ Missing.Weight; Missing.Height; Missing.GestationalAge ]
                            ] do
                            Readiness.Patient m |> missing |> Expect.isNone $"%A{m}"
                    }
                ]

            testList
                "notice"
                [
                    test "every page says the same for no data and no patient, then what it needs" {
                        for n in needs do
                            let noData = Readiness.NoData |> notice n |> Option.map List.head
                            let noPatient = Readiness.NoPatient |> notice n |> Option.map List.head

                            noData |> Expect.equal $"%A{n}" (Some Terms.``Patient enter patient data``)

                            noPatient
                            |> Expect.equal $"%A{n}" (Some Terms.``Patient enter age or weight and height``)
                    }

                    test "a page that browses names what needs the patient" {
                        Readiness.NoData
                        |> notice Needs.DoseCheck
                        |> Expect.equal
                            "the dose check"
                            (Some [ Terms.``Patient enter patient data``; Terms.``Patient Needed Dose Check`` ])

                        Readiness.NoPatient
                        |> notice Needs.PlanMedication
                        |> Expect.equal
                            "the plan's medication"
                            (Some
                                [
                                    Terms.``Patient enter age or weight and height``
                                    Terms.``Patient Needed Plan Medication``
                                ])
                    }

                    test "a page that browses says nothing once there is a patient, whatever it misses" {
                        for n in [ Needs.DoseCheck; Needs.PlanMedication ] do
                            for m in [ []; [ Missing.Age ]; [ Missing.Weight; Missing.Height ] ] do
                                Readiness.Patient m |> notice n |> Expect.isNone $"%A{n} %A{m}"
                    }

                    test "a page that calculates names every missing dimension, weight and height as one" {
                        let cases =
                            [
                                [ Missing.Age ], [ Terms.``Patient Age unknown`` ]
                                [ Missing.Weight; Missing.Height ], [ Terms.``Prescribe Weight and height unknown`` ]
                                [ Missing.Weight ], [ Terms.``Prescribe Weight unknown`` ]
                                [ Missing.Height ], [ Terms.``Prescribe Height unknown`` ]
                                [ Missing.GestationalAge ], [ Terms.``Patient Gestational age unknown`` ]
                                [ Missing.Weight; Missing.Height; Missing.GestationalAge ],
                                [
                                    Terms.``Prescribe Weight and height unknown``
                                    Terms.``Patient Gestational age unknown``
                                ]
                            ]

                        for m, expected in cases do
                            Readiness.Patient m
                            |> notice Needs.Calculation
                            |> Expect.equal $"%A{m}" (Some expected)
                    }

                    test "a complete patient gets no notice on any page" {
                        for n in needs do
                            Readiness.Patient [] |> notice n |> Expect.isNone $"%A{n}"
                    }
                ]

            testList
                "english and message"
                [
                    test "every term a notice can name has an English sentence" {
                        let allMissing = [ Missing.Age; Missing.Weight; Missing.Height; Missing.GestationalAge ]

                        let terms =
                            [
                                for n in needs do
                                    for r in [ Readiness.NoData; Readiness.NoPatient; Readiness.Patient allMissing ] do
                                        match r |> notice n with
                                        | Some x -> yield! x
                                        | None -> ()
                                yield! sentences [ Missing.Weight ]
                                yield! sentences [ Missing.Height ]
                            ]
                            |> List.distinct

                        for term in terms do
                            term |> english |> Expect.notEqual $"%A{term}" $"{term}"
                    }

                    test "the message joins the translated sentences" {
                        [ Terms.``Patient enter patient data``; Terms.``Patient Needed Dose Check`` ]
                        |> message named
                        |> Expect.equal "two sentences" "<Patient enter patient data> <Patient Needed Dose Check>"
                    }
                ]
        ]
