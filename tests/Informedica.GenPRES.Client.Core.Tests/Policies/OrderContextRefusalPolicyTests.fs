/// The refusal's notice policy, linked in from the client project.
module Informedica.GenPRES.Client.Core.Tests.Policies.OrderContextRefusalPolicyTests

open Informedica.GenPRES.Client.Core.Models
open Informedica.GenPRES.Client.Core.Policies
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types
open OrderContextRefusalPolicy


/// A translator that shows which term was asked for, so a test can assert terms, not prose.
let named (term: Terms) = $"<{term}>"


/// A patient the rules can match: a weight and a height.
let measured =
    { Models.Patient.empty with
        Weight = { Models.Patient.empty.Weight with Measured = Some 12000<gram> }
        Height = { Models.Patient.empty.Height with Measured = Some 90<cm> }
    }


/// The term column and the English column of the localization file, read when a test first asks.
let englishRows =
    lazy
        System.IO.Path.Combine(__SOURCE_DIRECTORY__, "../../../data/localization/localization.tsv")
        |> System.IO.File.ReadAllLines
        |> Array.map (fun line -> line.Split '\t')
        |> Array.filter (fun cells -> cells.Length > 1)
        |> Array.map (fun cells -> cells[0], cells[1])
        |> Map.ofArray


/// Every term the refusal's English covers.
let refusalTerms =
    [
        Terms.``Prescribe Refusal``
        Terms.``Prescribe Refusal No dose rules``
        Terms.``Prescribe Refusal Patient``
        Terms.``Prescribe Refusal No products``
        Terms.``Prescribe Refusal Measurement``
        Terms.``Prescribe Refusal Not solved``
        Terms.``Prescribe Refusal No adjusted dose``
        Terms.``Prescribe Refusal Contact``
    ]


let salbutamol =
    { Models.OrderContext.empty with
        Filter =
            { Models.OrderContext.filter with
                Generic = Some "salbutamol"
                Route = Some "intraveneus"
            }
        Patient = measured
    }


[<Tests>]
let tests =
    testList
        "the refusal's notice"
        [
            test "the English of every refusal term is a sentence, the bodies take the picks" {
                for term in refusalTerms do
                    english term |> Expect.notEqual $"{term} has English" $"{term}"

                for refusal in
                    [
                        OrderContextRefusal.NoDoseRules
                        OrderContextRefusal.NoDoseRulesForPatient
                        OrderContextRefusal.NoProducts
                        OrderContextRefusal.MeasurementMissing
                    ] do
                    (english (bodyTerm refusal)).Contains "{0}"
                    |> Expect.isTrue $"{refusal} names the picks"

                (english Terms.``Prescribe Refusal``).Contains "{0}"
                |> Expect.isFalse "the title takes none"
                (english Terms.``Prescribe Refusal Contact``).Contains "{0}"
                |> Expect.isFalse "the contact takes none"

                (english Terms.``Prescribe Refusal No products``).Contains "dose type"
                |> Expect.isTrue "the products body names the dose type too"
            }

            test "the picks are named in the page's order, as far as chosen" {
                picks salbutamol.Filter
                |> Expect.equal "generic, route" "salbutamol, intraveneus"

                { salbutamol.Filter with
                    Indication = Some "bronchospasme"
                    DoseType = Some(DoseType.Continuous "continu")
                }
                |> picks
                |> Expect.equal "all five in order" "bronchospasme, salbutamol, intraveneus, continu"

                Models.OrderContext.filter |> picks |> Expect.equal "nothing chosen" ""
            }

            test "the notice is the title, the body of the case with the picks, and the contact" {
                let n =
                    notice english salbutamol OrderContextRefusal.NoDoseRulesForPatient
                    |> Expect.wantSome "a notice"

                n.Title |> Expect.equal "the title" "No dose can be shown"

                n.Body
                |> Expect.equal
                    "the body"
                    "There are dose rules for salbutamol, intraveneus, but none covers the age, weight or department of this patient"

                n.Contact
                |> Expect.equal
                    "the contact"
                    "Report this to the pharmacy or the application manager, so that the rule can be added"
            }

            test "each case asks for its own term" {
                [
                    OrderContextRefusal.NoDoseRules
                    OrderContextRefusal.NoDoseRulesForPatient
                    OrderContextRefusal.NoProducts
                ]
                |> List.map (fun r -> (notice named salbutamol r |> Expect.wantSome "a notice").Body)
                |> Expect.equal
                    "the three bodies"
                    [
                        "<Prescribe Refusal No dose rules>"
                        "<Prescribe Refusal Patient>"
                        "<Prescribe Refusal No products>"
                    ]
            }

            test "the English of every refusal term is the English of the localization file" {
                refusalTerms
                |> List.filter (fun term -> englishRows.Value |> Map.tryFind $"{term}" <> Some(english term))
                |> Expect.isEmpty "the same English"
            }

            for refusal, term in
                [
                    OrderContextRefusal.NotSolved, Terms.``Prescribe Refusal Not solved``
                    OrderContextRefusal.NoAdjustedDose, Terms.``Prescribe Refusal No adjusted dose``
                ] do
                test $"a value that cannot be applied gets no notice; the snackbar says it without picks: %A{refusal}" {
                    notice english salbutamol refusal |> Expect.isNone "the order stays, no notice"

                    bodyTerm refusal |> Expect.equal "its own term" term

                    (english term).Contains "{0}" |> Expect.isFalse "takes no picks"
                }

            test "a missing measurement names the picks and no one to tell" {
                let n =
                    notice english salbutamol OrderContextRefusal.MeasurementMissing
                    |> Expect.wantSome "a notice"

                n.Body
                |> Expect.equal
                    "the body"
                    "The dose rules for salbutamol, intraveneus need a weight, or a weight and a height, that this patient does not have. Enter them to see a dose"

                n.Contact |> Expect.equal "no one to tell" ""
                n |> message |> Expect.equal "the message is the body alone" n.Body
            }

            test "a patient the rules are not looked up for gets no notice for the patient case" {
                // the page's own notice above the picks already says what to enter
                let incomplete = { salbutamol with Patient = Models.Patient.empty }

                notice english incomplete OrderContextRefusal.NoDoseRulesForPatient
                |> Expect.isNone "nothing to add to the missing weight"

                notice english incomplete OrderContextRefusal.NoDoseRules
                |> Expect.isSome "no rule at all is said whatever the patient"

                notice english salbutamol OrderContextRefusal.NoDoseRulesForPatient
                |> Expect.isSome "with a weight and a height the patient case is said"

                let noHeight = { measured with Height = Models.Patient.empty.Height }

                notice english { salbutamol with Patient = noHeight } OrderContextRefusal.NoDoseRulesForPatient
                |> Expect.isSome "a height alone missing: the rules are looked up, so the patient case is said"
            }

            test "the message is the body then the contact, an empty contact adding nothing" {
                let n =
                    notice english salbutamol OrderContextRefusal.NoDoseRules
                    |> Expect.wantSome "a notice"

                message n |> Expect.equal "both" $"{n.Body} {n.Contact}"
                message { n with Contact = "" } |> Expect.equal "the body alone" n.Body
            }
        ]
