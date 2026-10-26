namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// What an edit of the patient panel does to the draft, and what the App holds after it.
module PatientDraftPolicyTests =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Policies

    open Expecto
    open Expecto.Flip
    open PatientDraftPolicy


    module Fixtures =

        open Informedica.GenPRES.Shared.Types
        open Informedica.GenPRES.Shared.Models


        let row sex age p3 mean p97 : NormalValue =
            {
                Sex = sex
                Age = age
                P3 = p3
                Mean = mean
                P97 = p97
            }


        // the same rows for both sexes, so that a change of gender keeps the estimates
        let weights = Some [ row "M" 10. 25. 32. 40.; row "F" 10. 25. 32. 40. ]
        let heights = Some [ row "M" 10. 130. 140. 150.; row "F" 10. 130. 140. 150. ]


        /// What the App does with every draft it receives: the estimates filled again.
        let appStep (draft: Patient option) =
            draft |> Option.map (Patient.applyNormalValues weights heights None None)


        /// What the App holds after the panel's edit: estimated again after an edit of the age,
        /// the gender or the gestational age (UpdatePatient), taken as sent after any other
        /// (EditPatient).
        let afterEdit msg (draft: Patient option) =
            let edited = draft |> update msg

            match estimates msg with
            | Estimates.Renewed -> edited |> appStep
            | Estimates.Kept -> edited |> keepEstimates msg draft


        /// A ten-year-old boy as the App holds him: the age entered, weight and height estimated.
        let estimatedBoy =
            None
            |> PatientEdit.setYear (Some "10")
            |> PatientEdit.setGender "male"
            |> appStep


        /// The same boy with a measured weight; the height is still estimated.
        let weighedBoy = estimatedBoy |> PatientEdit.setWeight (Some "30000") |> appStep


        let weight (draft: Patient option) = draft |> Option.map _.Weight

        let height (draft: Patient option) = draft |> Option.map _.Height

        let estimatedWeight (draft: Patient option) = draft |> Option.bind _.Weight.Estimated

        let estimatedHeight (draft: Patient option) = draft |> Option.bind _.Height.Estimated

        let measuredWeight (draft: Patient option) = draft |> Option.bind _.Weight.Measured

        let measuredHeight (draft: Patient option) = draft |> Option.bind _.Height.Measured


    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Fixtures


    let fixtureTests =
        testList
            "the fixtures"
            [
                test "the estimated boy has both estimates and no measurement" {
                    (estimatedWeight estimatedBoy, estimatedHeight estimatedBoy)
                    |> Expect.equal "32 kg and 140 cm" (Some 32000<gram>, Some 140<cm>)

                    (measuredWeight estimatedBoy, measuredHeight estimatedBoy)
                    |> Expect.equal "nothing measured" (None, None)
                }

                test "the weighed boy has a measured weight and an estimated height" {
                    (measuredWeight weighedBoy, estimatedHeight weighedBoy)
                    |> Expect.equal "30 kg measured, 140 cm estimated" (Some 30000<gram>, Some 140<cm>)
                }
            ]


    // The App's round trip can give back the draft it had, so a hook keyed on that draft is not
    // re-seeded: the reason the panel keeps no copy of the draft.
    let roundTripTests =
        testList
            "the App's round trip"
            [
                test "an estimated height cleared gives back the draft the App had" {
                    estimatedBoy
                    |> PatientEdit.setHeight None
                    |> appStep
                    |> Expect.equal "the same draft" estimatedBoy
                }

                test "an estimated weight cleared gives back the draft the App had" {
                    estimatedBoy
                    |> PatientEdit.setWeight None
                    |> appStep
                    |> Expect.equal "the same draft" estimatedBoy
                }

                test "the measured weight chosen again gives back the draft the App had" {
                    weighedBoy
                    |> PatientEdit.setWeight (Some "30000")
                    |> appStep
                    |> Expect.equal "the same draft" weighedBoy
                }

                test "a measured height cleared brings the height estimate back and keeps the measured weight" {
                    let measured = weighedBoy |> PatientEdit.setHeight (Some "135") |> appStep
                    let cleared = measured |> PatientEdit.setHeight None |> appStep

                    (measuredWeight cleared, measuredHeight cleared, estimatedHeight cleared)
                    |> Expect.equal
                        "30 kg kept, no measured height, 140 cm estimated"
                        (Some 30000<gram>, None, Some 140<cm>)

                    cleared |> Expect.equal "the weighed boy again" weighedBoy
                }
            ]


    // Why the panel may keep no copy: the copy an edit leaves behind is not what the App holds,
    // while the App's draft does not change.
    let staleCopyTests =
        testList
            "a copy of the draft in the panel"
            [
                test "an estimated height cleared leaves a copy without either estimate" {
                    let copy = estimatedBoy |> update (Msg.UpdateHeight None)

                    (estimatedWeight copy, estimatedHeight copy)
                    |> Expect.equal "the copy shows no weight and no height" (None, None)

                    copy
                    |> appStep
                    |> Expect.equal "while the App's draft is unchanged" estimatedBoy
                }

                test "the measured weight chosen again leaves a copy without the height estimate" {
                    let copy = weighedBoy |> update (Msg.UpdateWeight(Some "30000"))

                    (measuredWeight copy, estimatedHeight copy)
                    |> Expect.equal "the copy shows the weight, not the height" (Some 30000<gram>, None)

                    copy |> appStep |> Expect.equal "while the App's draft is unchanged" weighedBoy
                }
            ]


    let allEdits =
        [
            Msg.UpdateYear(Some "8")
            Msg.UpdateMonth(Some "3")
            Msg.UpdateWeek(Some "2")
            Msg.UpdateDay(Some "4")
            Msg.UpdateWeight(Some "28000")
            Msg.UpdateWeight None
            Msg.UpdateHeight(Some "132")
            Msg.UpdateHeight None
            Msg.UpdateGAWeek(Some "32")
            Msg.UpdateGADay(Some "3")
            Msg.UpdateGender "female"
            Msg.UpdateRenal None
            Msg.UpdateDepartment(Some "ICK")
            Msg.ToggleCVL
            Msg.TogglePVL
            Msg.ToggleET
        ]


    // The edit alone: what the panel sends to the App. The setters blank both estimates, so the
    // edit's result is compared with the draft it was applied to, estimates blanked on both sides.
    let editTests =
        let blanked (draft: Patient option) = draft |> Option.map (Patient.withEstimates None None)

        testList
            "the edit alone"
            [
                test "the height cleared: no measured height, no estimates, the rest as it was" {
                    weighedBoy
                    |> update (Msg.UpdateHeight None)
                    |> Expect.equal "the weighed boy without estimates" (blanked weighedBoy)
                }

                test "the weight entered: the measured weight, no estimates, the rest as it was" {
                    let edited = estimatedBoy |> update (Msg.UpdateWeight(Some "28000"))

                    measuredWeight edited |> Expect.equal "28 kg measured" (Some 28000<gram>)

                    edited
                    |> PatientEdit.setWeight None
                    |> Expect.equal "otherwise the estimated boy without estimates" (blanked estimatedBoy)
                }

                test "the department chosen: only the department changes" {
                    weighedBoy
                    |> update (Msg.UpdateDepartment(Some "ICK"))
                    |> Option.map _.Department
                    |> Expect.equal "ICK" (Some(Some "ICK"))
                }

                test "the draft cleared: no draft" { weighedBoy |> update Msg.Clear |> Expect.isNone "no draft" }

                test "no department on no draft stays no draft" {
                    None |> update (Msg.UpdateDepartment None) |> Expect.isNone "no draft"
                }

                testList
                    "every edit keeps the measured weight it does not change"
                    [
                        for msg in allEdits do
                            match msg with
                            | Msg.UpdateWeight _ -> ()
                            | _ ->
                                test $"%A{msg}" {
                                    weighedBoy
                                    |> update msg
                                    |> measuredWeight
                                    |> Expect.equal "30 kg measured" (Some 30000<gram>)
                                }
                    ]
            ]


    // What the App holds after the panel's edit, and so what the panel shows.
    let afterEditTests =
        testList
            "what the App holds after the panel's edit"
            [
                test "an estimated weight cleared: no weight, the height estimate stays" {
                    let held = estimatedBoy |> afterEdit (Msg.UpdateWeight None)

                    (weight held |> Option.map (fun w -> w.Measured, w.Estimated), estimatedHeight held)
                    |> Expect.equal "no weight, 140 cm" (Some(None, None), Some 140<cm>)
                }

                test "an estimated height cleared: no height, the weight estimate stays" {
                    let held = estimatedBoy |> afterEdit (Msg.UpdateHeight None)

                    (estimatedWeight held, height held |> Option.map (fun h -> h.Measured, h.Estimated))
                    |> Expect.equal "32 kg, no height" (Some 32000<gram>, Some(None, None))
                }

                test "a measured weight cleared: no weight, no estimate in its place" {
                    let held = weighedBoy |> afterEdit (Msg.UpdateWeight None)

                    (measuredWeight held, estimatedWeight held, estimatedHeight held)
                    |> Expect.equal "no weight, 140 cm" (None, None, Some 140<cm>)
                }

                test "a weight entered: the weight measured, the height estimate stays" {
                    let held = estimatedBoy |> afterEdit (Msg.UpdateWeight(Some "28000"))

                    (measuredWeight held, estimatedHeight held)
                    |> Expect.equal "28 kg measured, 140 cm" (Some 28000<gram>, Some 140<cm>)
                }

                test "a cleared weight stays cleared over an edit of the department" {
                    estimatedBoy
                    |> afterEdit (Msg.UpdateWeight None)
                    |> afterEdit (Msg.UpdateDepartment(Some "ICK"))
                    |> weight
                    |> Option.map (fun w -> w.Measured, w.Estimated)
                    |> Expect.equal "no weight" (Some(None, None))
                }

                test "a cleared weight is estimated again after an edit of the age" {
                    estimatedBoy
                    |> afterEdit (Msg.UpdateWeight None)
                    |> afterEdit (Msg.UpdateMonth(Some "1"))
                    |> estimatedWeight
                    |> Expect.equal "32 kg" (Some 32000<gram>)
                }

                test "a cleared weight is estimated again after an edit of the gender" {
                    estimatedBoy
                    |> afterEdit (Msg.UpdateWeight None)
                    |> afterEdit (Msg.UpdateGender "female")
                    |> estimatedWeight
                    |> Expect.equal "32 kg" (Some 32000<gram>)
                }

                test "a measured weight stays measured after an edit of the age" {
                    weighedBoy
                    |> afterEdit (Msg.UpdateYear(Some "11"))
                    |> measuredWeight
                    |> Expect.equal "30 kg" (Some 30000<gram>)
                }

                testList
                    "every edit that keeps the estimates leaves weight and height as they were"
                    [
                        for msg in allEdits do
                            match msg with
                            | Msg.UpdateWeight _
                            | Msg.UpdateHeight _ -> ()
                            | _ when estimates msg = Estimates.Kept ->
                                test $"%A{msg}" {
                                    let held = weighedBoy |> afterEdit msg

                                    (weight held, height held)
                                    |> Expect.equal "the same weight and height" (weight weighedBoy, height weighedBoy)
                                }
                            | _ -> ()
                    ]
            ]


    let canCalculateTests =
        testList
            "canCalculate"
            [
                test "no draft is no patient" { None |> canCalculate |> Expect.isFalse "no patient" }

                test "an age alone is a patient, its weight and height estimated" {
                    estimatedBoy |> canCalculate |> Expect.isTrue "a patient"
                }

                test "a measured weight alone is no patient" {
                    None
                    |> PatientEdit.setWeight (Some "30000")
                    |> canCalculate
                    |> Expect.isFalse "no patient"
                }

                test "a measured weight and height are a patient" {
                    None
                    |> PatientEdit.setWeight (Some "30000")
                    |> PatientEdit.setHeight (Some "135")
                    |> canCalculate
                    |> Expect.isTrue "a patient"
                }
            ]


    [<Tests>]
    let tests =
        testList
            "PatientDraftPolicy"
            [
                fixtureTests
                roundTripTests
                staleCopyTests
                editTests
                afterEditTests
                canCalculateTests
            ]
