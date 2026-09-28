/// The refusal's notice policy, linked in from the client project.
module Informedica.GenPRES.Client.Core.Tests.OrderContextRefusalPolicyTests

open Expecto
open Expecto.Flip
open Shared
open Shared.Types
open OrderContextRefusalPolicy


/// A translator that shows which term was asked for, so a test can assert terms, not prose.
let named (term: Terms) = $"<{term}>"


/// A patient the rules can match: a weight and a height.
let measured =
    { Models.Patient.empty with
        Weight = { Models.Patient.empty.Weight with Measured = Some 12000<gram> }
        Height = { Models.Patient.empty.Height with Measured = Some 90<cm> }
    }


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
                for term in
                    [
                        Terms.``Prescribe Refusal``
                        Terms.``Prescribe Refusal No dose rules``
                        Terms.``Prescribe Refusal Patient``
                        Terms.``Prescribe Refusal No products``
                        Terms.``Prescribe Refusal Contact``
                    ] do
                    english term |> Expect.notEqual $"{term} has English" $"{term}"

                for refusal in
                    [
                        OrderContextRefusal.NoDoseRules
                        OrderContextRefusal.NoDoseRulesForPatient
                        OrderContextRefusal.NoProducts
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

            test "a patient without a weight or a height gets no notice for the patient case" {
                // the page's own notice above the picks already says what to enter
                let incomplete = { salbutamol with Patient = Models.Patient.empty }

                notice english incomplete OrderContextRefusal.NoDoseRulesForPatient
                |> Expect.isNone "nothing to add to the missing weight and height"

                notice english incomplete OrderContextRefusal.NoDoseRules
                |> Expect.isSome "no rule at all is said whatever the patient"

                notice english salbutamol OrderContextRefusal.NoDoseRulesForPatient
                |> Expect.isSome "with a weight and a height the patient case is said"

                let noHeight = { measured with Height = Models.Patient.empty.Height }

                notice english { salbutamol with Patient = noHeight } OrderContextRefusal.NoDoseRulesForPatient
                |> Expect.isNone "a height alone missing"
            }

            test "the message is the body then the contact, an empty contact adding nothing" {
                let n =
                    notice english salbutamol OrderContextRefusal.NoDoseRules
                    |> Expect.wantSome "a notice"

                message n |> Expect.equal "both" $"{n.Body} {n.Contact}"
                message { n with Contact = "" } |> Expect.equal "the body alone" n.Body
            }
        ]
