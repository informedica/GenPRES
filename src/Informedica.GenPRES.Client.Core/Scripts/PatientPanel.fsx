// Step 1 of the plan for #1152: the patient panel's edits as a pure module, and the round trip
// they have to survive.
//
// The panel in Views/Patient.fs keeps its own copy of the draft in a useElmish hook. An edit
// blanks both estimates in that copy, the App estimates them again, and when the result equals
// the draft it had, the hook is not re-seeded: the panel shows the blanked copy while the doses
// rest on the estimates. The fix lets the panel show the App's draft and keep no copy of it.
//
// The rule the panel follows: the weight and height the user did not enter are estimated after
// an edit of the age, the gender or the gestational age, and only then. After any other edit
// the estimates stay as they were, so a weight or a height the user cleared stays cleared.
//
// The module below is `PatientPanel.fs` as it becomes in Client.Core: the edit reducer of
// Views/Patient.fs without the dispatch, so that each edit is a function of the App's draft
// alone. The tests at the end migrate to `PatientPanelTests.fs` in the Client.Core tests.
//
// Run: `dotnet fsi PatientPanel.fsx` from this directory, after `dotnet run build`.

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "nuget: Expecto"

open Expecto
open Expecto.Flip


/// The edits of the patient panel, as pure functions of the draft the App holds. The panel
/// keeps no copy of the draft: it applies an edit to the App's draft and sends the result
/// back, and the App fills the estimates again.
module PatientPanel =

    open Shared.Types
    open Shared.Models


    /// One edit the panel makes to the draft.
    [<RequireQualifiedAccess>]
    type Msg =
        /// The draft discarded as a whole: the data of an anonymous patient, which is
        /// fictitious; an identified patient's data is changed, never cleared.
        | Clear
        | UpdateYear of string option
        | UpdateMonth of string option
        | UpdateWeek of string option
        | UpdateDay of string option
        | UpdateWeight of string option
        | UpdateHeight of string option
        | UpdateGAWeek of string option
        | UpdateGADay of string option
        | UpdateGender of string
        | UpdateRenal of string option
        | UpdateDepartment of string option
        | ToggleCVL
        | TogglePVL
        | ToggleET


    /// The department chosen for the draft, or none, which leaves the server's default in
    /// force. Clearing what was never a draft stays no draft.
    let setDepartment (s: string option) (p: Patient option) : Patient option =
        match p, s with
        | None, None -> None
        | _ -> { (p |> Option.defaultValue Patient.empty) with Department = s } |> Some


    /// The draft after one edit. The setters blank both estimates, so the result is what the
    /// App receives, not what it shows: the App estimates it again.
    let update (msg: Msg) (draft: Patient option) : Patient option =
        match msg with
        | Msg.Clear -> None
        | Msg.UpdateYear s -> draft |> Patient.setYear s
        | Msg.UpdateMonth s -> draft |> Patient.setMonth s
        | Msg.UpdateWeek s -> draft |> Patient.setWeek s
        | Msg.UpdateDay s -> draft |> Patient.setDay s
        | Msg.UpdateWeight s -> draft |> Patient.setWeight s
        | Msg.UpdateHeight s -> draft |> Patient.setHeight s
        | Msg.UpdateGAWeek s -> draft |> Patient.setGAWeek s
        | Msg.UpdateGADay s -> draft |> Patient.setGADay s
        | Msg.UpdateRenal s -> draft |> Patient.setRenal s
        | Msg.UpdateGender s -> draft |> Patient.setGender s
        | Msg.UpdateDepartment s -> draft |> setDepartment s
        | Msg.ToggleCVL -> draft |> Patient.toggleCVL
        | Msg.TogglePVL -> draft |> Patient.togglePVL
        | Msg.ToggleET -> draft |> Patient.toggleET


    /// What becomes of the estimated weight and height after an edit.
    [<RequireQualifiedAccess>]
    type Estimates =
        /// Estimated again, for the weight and height the user did not enter: after an edit of
        /// the age, the gender or the gestational age, which the normal values follow.
        | Renewed
        /// Kept as they were: after any other edit, so that a weight or a height the user
        /// cleared stays cleared and is not estimated again.
        | Kept


    let estimates msg =
        match msg with
        | Msg.Clear
        | Msg.UpdateYear _
        | Msg.UpdateMonth _
        | Msg.UpdateWeek _
        | Msg.UpdateDay _
        | Msg.UpdateGender _
        | Msg.UpdateGAWeek _
        | Msg.UpdateGADay _ -> Estimates.Renewed
        | Msg.UpdateWeight _
        | Msg.UpdateHeight _
        | Msg.UpdateRenal _
        | Msg.UpdateDepartment _
        | Msg.ToggleCVL
        | Msg.TogglePVL
        | Msg.ToggleET -> Estimates.Kept


    /// The edited draft with the weight and the height the edit does not set as they were in the
    /// draft, estimates included. The setters blank both estimates; the measure an edit sets or
    /// clears keeps none, so a cleared weight or height shows nothing.
    let keepEstimates msg (draft: Patient option) (edited: Patient option) : Patient option =
        match draft, edited with
        | Some d, Some e ->
            match msg with
            | Msg.UpdateWeight _ -> { e with Height = d.Height }
            | Msg.UpdateHeight _ -> { e with Weight = d.Weight }
            | _ ->
                { e with
                    Weight = d.Weight
                    Height = d.Height
                }
            |> Some
        | _ -> edited


module Fixtures =

    open Shared.Types
    open Shared.Models


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


    /// What the App holds after the panel's edit: estimated again after an edit of the age, the
    /// gender or the gestational age (UpdatePatient), taken as it is after any other (EditPatient).
    let afterEdit msg (draft: Patient option) =
        let edited = draft |> PatientPanel.update msg

        match PatientPanel.estimates msg with
        | PatientPanel.Estimates.Renewed -> edited |> appStep
        | PatientPanel.Estimates.Kept -> edited |> PatientPanel.keepEstimates msg draft


    /// A ten-year-old boy as the App holds him: the age entered, weight and height estimated.
    let estimatedBoy =
        None
        |> Patient.setYear (Some "10")
        |> Patient.setGender "male"
        |> appStep


    /// The same boy with a measured weight; the height is still estimated.
    let weighedBoy = estimatedBoy |> Patient.setWeight (Some "30000") |> appStep


    let weight (draft: Patient option) = draft |> Option.map _.Weight

    let height (draft: Patient option) = draft |> Option.map _.Height

    let estimatedWeight (draft: Patient option) = draft |> Option.bind _.Weight.Estimated

    let estimatedHeight (draft: Patient option) = draft |> Option.bind _.Height.Estimated

    let measuredWeight (draft: Patient option) = draft |> Option.bind _.Weight.Measured

    let measuredHeight (draft: Patient option) = draft |> Option.bind _.Height.Measured


open Shared.Types
open Shared.Models
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


// The condition #1152 needs: the App's round trip gives back the draft it had, so a hook whose
// dependencies are that draft is not re-seeded.
let roundTripTests =
    testList
        "the App's round trip"
        [
            test "an estimated height cleared gives back the draft the App had" {
                estimatedBoy
                |> Patient.setHeight None
                |> appStep
                |> Expect.equal "the same draft" estimatedBoy
            }

            test "an estimated weight cleared gives back the draft the App had" {
                estimatedBoy
                |> Patient.setWeight None
                |> appStep
                |> Expect.equal "the same draft" estimatedBoy
            }

            test "the measured weight chosen again gives back the draft the App had" {
                weighedBoy
                |> Patient.setWeight (Some "30000")
                |> appStep
                |> Expect.equal "the same draft" weighedBoy
            }

            test "a measured height cleared brings the height estimate back and keeps the measured weight" {
                let measured = weighedBoy |> Patient.setHeight (Some "135") |> appStep
                let cleared = measured |> Patient.setHeight None |> appStep

                (measuredWeight cleared, measuredHeight cleared, estimatedHeight cleared)
                |> Expect.equal "30 kg kept, no measured height, 140 cm estimated" (Some 30000<gram>, None, Some 140<cm>)

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
                let copy = estimatedBoy |> PatientPanel.update (PatientPanel.Msg.UpdateHeight None)

                (estimatedWeight copy, estimatedHeight copy)
                |> Expect.equal "the copy shows no weight and no height" (None, None)

                copy
                |> appStep
                |> Expect.equal "while the App's draft is unchanged" estimatedBoy
            }

            test "the measured weight chosen again leaves a copy without the height estimate" {
                let copy = weighedBoy |> PatientPanel.update (PatientPanel.Msg.UpdateWeight(Some "30000"))

                (measuredWeight copy, estimatedHeight copy)
                |> Expect.equal "the copy shows the weight, not the height" (Some 30000<gram>, None)

                copy
                |> appStep
                |> Expect.equal "while the App's draft is unchanged" weighedBoy
            }
        ]


let allEdits =
    [
        PatientPanel.Msg.UpdateYear(Some "8")
        PatientPanel.Msg.UpdateMonth(Some "3")
        PatientPanel.Msg.UpdateWeek(Some "2")
        PatientPanel.Msg.UpdateDay(Some "4")
        PatientPanel.Msg.UpdateWeight(Some "28000")
        PatientPanel.Msg.UpdateWeight None
        PatientPanel.Msg.UpdateHeight(Some "132")
        PatientPanel.Msg.UpdateHeight None
        PatientPanel.Msg.UpdateGAWeek(Some "32")
        PatientPanel.Msg.UpdateGADay(Some "3")
        PatientPanel.Msg.UpdateGender "female"
        PatientPanel.Msg.UpdateRenal None
        PatientPanel.Msg.UpdateDepartment(Some "ICK")
        PatientPanel.Msg.ToggleCVL
        PatientPanel.Msg.TogglePVL
        PatientPanel.Msg.ToggleET
    ]


// The edit alone: what the panel sends to the App. The setters blank both estimates, so the
// edit's result is compared with the draft it was applied to, estimates blanked on both sides.
let editTests =
    let blanked (draft: Patient option) =
        draft |> Option.map (Patient.withEstimates None None)

    testList
        "the edit alone"
        [
            test "the height cleared: no measured height, no estimates, the rest as it was" {
                weighedBoy
                |> PatientPanel.update (PatientPanel.Msg.UpdateHeight None)
                |> Expect.equal "the weighed boy without estimates" (blanked weighedBoy)
            }

            test "the weight entered: the measured weight, no estimates, the rest as it was" {
                let edited = estimatedBoy |> PatientPanel.update (PatientPanel.Msg.UpdateWeight(Some "28000"))

                measuredWeight edited |> Expect.equal "28 kg measured" (Some 28000<gram>)

                edited
                |> Patient.setWeight None
                |> Expect.equal "otherwise the estimated boy without estimates" (blanked estimatedBoy)
            }

            test "the department chosen: only the department changes" {
                weighedBoy
                |> PatientPanel.update (PatientPanel.Msg.UpdateDepartment(Some "ICK"))
                |> Option.map _.Department
                |> Expect.equal "ICK" (Some(Some "ICK"))
            }

            test "the draft cleared: no draft" {
                weighedBoy
                |> PatientPanel.update PatientPanel.Msg.Clear
                |> Expect.isNone "no draft"
            }

            test "no department on no draft stays no draft" {
                None
                |> PatientPanel.update (PatientPanel.Msg.UpdateDepartment None)
                |> Expect.isNone "no draft"
            }

            testList "every edit keeps the measured weight it does not change" [
                for msg in allEdits do
                    match msg with
                    | PatientPanel.Msg.UpdateWeight _ -> ()
                    | _ ->
                        test $"%A{msg}" {
                            weighedBoy
                            |> PatientPanel.update msg
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
                let held = estimatedBoy |> afterEdit (PatientPanel.Msg.UpdateWeight None)

                (weight held |> Option.map (fun w -> w.Measured, w.Estimated), estimatedHeight held)
                |> Expect.equal "no weight, 140 cm" (Some(None, None), Some 140<cm>)
            }

            test "an estimated height cleared: no height, the weight estimate stays" {
                let held = estimatedBoy |> afterEdit (PatientPanel.Msg.UpdateHeight None)

                (estimatedWeight held, height held |> Option.map (fun h -> h.Measured, h.Estimated))
                |> Expect.equal "32 kg, no height" (Some 32000<gram>, Some(None, None))
            }

            test "a measured weight cleared: no weight, no estimate in its place" {
                let held = weighedBoy |> afterEdit (PatientPanel.Msg.UpdateWeight None)

                (measuredWeight held, estimatedWeight held, estimatedHeight held)
                |> Expect.equal "no weight, 140 cm" (None, None, Some 140<cm>)
            }

            test "a weight entered: the weight measured, the height estimate stays" {
                let held = estimatedBoy |> afterEdit (PatientPanel.Msg.UpdateWeight(Some "28000"))

                (measuredWeight held, estimatedHeight held)
                |> Expect.equal "28 kg measured, 140 cm" (Some 28000<gram>, Some 140<cm>)
            }

            test "a cleared weight stays cleared over an edit of the department" {
                estimatedBoy
                |> afterEdit (PatientPanel.Msg.UpdateWeight None)
                |> afterEdit (PatientPanel.Msg.UpdateDepartment(Some "ICK"))
                |> weight
                |> Option.map (fun w -> w.Measured, w.Estimated)
                |> Expect.equal "no weight" (Some(None, None))
            }

            test "a cleared weight is estimated again after an edit of the age" {
                estimatedBoy
                |> afterEdit (PatientPanel.Msg.UpdateWeight None)
                |> afterEdit (PatientPanel.Msg.UpdateMonth(Some "1"))
                |> estimatedWeight
                |> Expect.equal "32 kg" (Some 32000<gram>)
            }

            test "a cleared weight is estimated again after an edit of the gender" {
                estimatedBoy
                |> afterEdit (PatientPanel.Msg.UpdateWeight None)
                |> afterEdit (PatientPanel.Msg.UpdateGender "female")
                |> estimatedWeight
                |> Expect.equal "32 kg" (Some 32000<gram>)
            }

            test "a measured weight stays measured after an edit of the age" {
                weighedBoy
                |> afterEdit (PatientPanel.Msg.UpdateYear(Some "11"))
                |> measuredWeight
                |> Expect.equal "30 kg" (Some 30000<gram>)
            }

            testList "every edit that keeps the estimates leaves weight and height as they were" [
                for msg in allEdits do
                    match msg with
                    | PatientPanel.Msg.UpdateWeight _
                    | PatientPanel.Msg.UpdateHeight _ -> ()
                    | _ when PatientPanel.estimates msg = PatientPanel.Estimates.Kept ->
                        test $"%A{msg}" {
                            let held = weighedBoy |> afterEdit msg

                            (weight held, height held)
                            |> Expect.equal "the same weight and height" (weight weighedBoy, height weighedBoy)
                        }
                    | _ -> ()
            ]
        ]


let tests =
    testList
        "PatientPanel"
        [
            fixtureTests
            roundTripTests
            staleCopyTests
            editTests
            afterEditTests
        ]


runTestsWithCLIArgs [] [||] tests
