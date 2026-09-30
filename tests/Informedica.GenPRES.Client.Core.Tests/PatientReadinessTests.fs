/// What the pages can do with the patient draft, and what they say when it is not enough.
module Informedica.GenPRES.Client.Core.Tests.PatientReadinessTests

open Expecto
open Expecto.Flip
open Shared
open Shared.Types
open PatientReadiness


/// A translator that shows which term was asked for, so a test can assert terms, not prose.
let named (term: Terms) = $"<{term}>"


let empty = Models.Patient.empty


/// An age of five years, nothing else.
let aged = Models.Patient.setYear (Some "5") None


let withWeight (p: Patient option) = p |> Models.Patient.setWeight (Some "18000")


let withHeight (p: Patient option) = p |> Models.Patient.setHeight (Some "110")


/// An age with the weight and the height estimated, as the App fills them in.
let estimated =
    aged
    |> Option.map (fun p ->
        { p with
            Weight = { p.Weight with Estimated = Some 18000<gram> }
            Height = { p.Height with Estimated = Some 110<cm> }
        }
    )


let readinesses =
    [
        Readiness.NoData
        Readiness.NoPatient
        Readiness.NoWeightAndHeight
        Readiness.NoWeight
        Readiness.NoHeight
        Readiness.NoAge
        Readiness.Complete
    ]


let needs = [ Needs.Weight; Needs.DoseRules; Needs.DoseCheck; Needs.PlanMedication ]


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
                                Models.Patient.setGender "male" None
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

                    test "a draft with a weight and a height and no age has no age" {
                        None
                        |> withWeight
                        |> withHeight
                        |> readiness
                        |> Expect.equal "a patient by its measurements" Readiness.NoAge
                    }

                    test "an age alone has no weight and no height" {
                        aged
                        |> readiness
                        |> Expect.equal "nothing to estimate from yet" Readiness.NoWeightAndHeight
                    }

                    test "an age with a measured height has no weight" {
                        aged |> withHeight |> readiness |> Expect.equal "the weight" Readiness.NoWeight
                    }

                    test "an age with a measured weight has no height" {
                        aged |> withWeight |> readiness |> Expect.equal "the height" Readiness.NoHeight
                    }

                    test "an age with both estimated is complete" {
                        estimated |> readiness |> Expect.equal "estimates count" Readiness.Complete
                    }

                    test "an age with both measured is complete" {
                        aged
                        |> withWeight
                        |> withHeight
                        |> readiness
                        |> Expect.equal "measured" Readiness.Complete
                    }

                    test "a cleared weight over an estimate stays no weight" {
                        // the setter blanks the estimates, as the panel's edit does
                        estimated
                        |> Models.Patient.setWeight None
                        |> readiness
                        |> Expect.equal "no estimate stands in" Readiness.NoWeightAndHeight
                    }
                ]

            testList
                "canCalculate"
                [
                    test "agrees with the draft policy for every fixture" {
                        for draft in [ None; Some empty; aged; aged |> withWeight; None |> withWeight; estimated ] do
                            draft
                            |> canCalculate
                            |> Expect.equal $"%A{draft}" (draft |> PatientDraftPolicy.canCalculate)
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

                    test "a patient misses nothing" {
                        for r in
                            readinesses
                            |> List.filter (fun r -> r <> Readiness.NoData && r <> Readiness.NoPatient) do
                            r |> missing |> Expect.isNone $"%A{r}"
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

                    test "a page that browses says nothing once there is a patient" {
                        for n in [ Needs.DoseCheck; Needs.PlanMedication ] do
                            for r in readinesses |> List.filter (missing >> Option.isNone) do
                                r |> notice n |> Expect.isNone $"%A{n} %A{r}"
                    }

                    test "the dose rules name the missing dimension" {
                        let expected r =
                            match r with
                            | Readiness.NoWeightAndHeight -> Terms.``Prescribe Weight and height unknown``
                            | Readiness.NoWeight -> Terms.``Prescribe Weight unknown``
                            | Readiness.NoHeight -> Terms.``Prescribe Height unknown``
                            | _ -> Terms.``Prescribe Age unknown``

                        for r in
                            [
                                Readiness.NoWeightAndHeight
                                Readiness.NoWeight
                                Readiness.NoHeight
                                Readiness.NoAge
                            ] do
                            r |> notice Needs.DoseRules |> Expect.equal $"%A{r}" (Some [ expected r ])
                    }

                    test "the weight pages ask for the weight only" {
                        for r in [ Readiness.NoWeightAndHeight; Readiness.NoWeight ] do
                            r
                            |> notice Needs.Weight
                            |> Expect.equal $"%A{r}" (Some [ Terms.``Prescribe Weight unknown`` ])

                        for r in [ Readiness.NoHeight; Readiness.NoAge ] do
                            r |> notice Needs.Weight |> Expect.isNone $"%A{r}"
                    }

                    test "a complete patient gets no notice on any page" {
                        for n in needs do
                            Readiness.Complete |> notice n |> Expect.isNone $"%A{n}"
                    }
                ]

            testList
                "english and message"
                [
                    test "every term a notice can name has an English sentence" {
                        let terms =
                            [
                                for n in needs do
                                    for r in readinesses do
                                        match r |> notice n with
                                        | Some x -> yield! x
                                        | None -> ()
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
