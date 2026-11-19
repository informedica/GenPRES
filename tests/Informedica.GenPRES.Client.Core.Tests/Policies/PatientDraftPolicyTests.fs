namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// The edit that a message of the patient panel makes, and whether the draft is a patient.
module PatientDraftPolicyTests =

    open Informedica.GenPRES.Client.Core.Policies

    open Expecto
    open Expecto.Flip
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
    open PatientDraftPolicy


    module Fixtures =

        module Models = Informedica.GenPRES.Shared.Models

        let empty = Models.PatientData.empty

        /// A ten-year-old boy, nothing measured.
        let boy =
            { empty with
                Age = Some { Models.PatientData.Age.ageZero with Years = 10<year> }
                Gender = Male
            }

        /// Patient data with a measured weight of 30 kg and nothing else.
        let weighed = { empty with Weight = { empty.Weight with Measured = Some 30000<gram> } }


    open Fixtures


    let toEditTests =
        testList
            "toEdit"
            [
                test "the fields become typed edits, an empty or invalid text None" {
                    [
                        Msg.UpdateYear(Some "10")
                        Msg.UpdateMonth None
                        Msg.UpdateWeek(Some "2")
                        Msg.UpdateDay(Some "3")
                        Msg.UpdateWeight(Some "12340")
                        Msg.UpdateHeight(Some "abc")
                        Msg.UpdateGAWeek(Some "36")
                        Msg.UpdateGADay(Some "4")
                        Msg.UpdateGender Female
                        Msg.UpdateDepartment(Some "ICK")
                        Msg.UpdateMarkedAdult true
                    ]
                    |> List.map (toEdit (Some boy))
                    |> Expect.equal
                        "typed"
                        [
                            Some(PatientDataEdit.SetYears(Some 10))
                            Some(PatientDataEdit.SetMonths None)
                            Some(PatientDataEdit.SetWeeks(Some 2))
                            Some(PatientDataEdit.SetDays(Some 3))
                            Some(PatientDataEdit.SetWeight(Some 12340<gram>))
                            Some(PatientDataEdit.SetHeight None)
                            Some(PatientDataEdit.SetGestationalWeeks(Some 36))
                            Some(PatientDataEdit.SetGestationalDays(Some 4))
                            Some(PatientDataEdit.SetGender Female)
                            Some(PatientDataEdit.SetDepartment(Some "ICK"))
                            Some(PatientDataEdit.SetMarkedAdult true)
                        ]
                }

                test "an empty renal function is a clear" {
                    Msg.UpdateRenal None
                    |> toEdit (Some boy)
                    |> Expect.equal "clear" (Some(PatientDataEdit.SetRenalFunction None))
                }

                test "a switch adds an access the draft lacks and removes one it has" {
                    (Msg.ToggleCVL |> toEdit (Some boy),
                     Msg.ToggleCVL |> toEdit (Some { boy with Access = [ CVL ] }),
                     Msg.ToggleET |> toEdit None)
                    |> Expect.equal
                        "add, remove, add"
                        (Some(PatientDataEdit.AddAccess CVL),
                         Some(PatientDataEdit.RemoveAccess CVL),
                         Some(PatientDataEdit.AddAccess EnteralTube))
                }

                test "the reset is no edit" { Msg.Clear |> toEdit (Some boy) |> Expect.isNone "none" }
            ]


    let canCalculateTests =
        testList
            "canCalculate"
            [
                test "no draft is no patient" { None |> canCalculate |> Expect.isFalse "no patient" }

                test "an age alone is a patient" { Some boy |> canCalculate |> Expect.isTrue "a patient" }

                test "a measured weight alone is no patient" {
                    Some weighed |> canCalculate |> Expect.isFalse "no patient"
                }

                test "a measured weight and height are a patient" {
                    Some { weighed with Height = { weighed.Height with Measured = Some 135<cm> } }
                    |> canCalculate
                    |> Expect.isTrue "a patient"
                }
            ]


    [<Tests>]
    let tests = testList "PatientDraftPolicy" [ toEditTests; canCalculateTests ]
