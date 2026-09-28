/// The refusal's notice policy, linked in from the client project.
module Informedica.GenPRES.Client.Core.Tests.OrderContextRefusalPolicyTests

open Expecto
open Expecto.Flip
open Shared
open Shared.Types
open OrderContextRefusalPolicy


/// A translator that shows which term was asked for, so a test can assert terms, not prose.
let named (term: Terms) = $"<{term}>"


let salbutamol =
    { Models.OrderContext.empty with
        Filter =
            { Models.OrderContext.filter with
                Generic = Some "salbutamol"
                Route = Some "intraveneus"
            }
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
                let n = notice english salbutamol OrderContextRefusal.NoDoseRulesForPatient

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
                |> List.map (fun r -> (notice named salbutamol r).Body)
                |> Expect.equal
                    "the three bodies"
                    [
                        "<Prescribe Refusal No dose rules>"
                        "<Prescribe Refusal Patient>"
                        "<Prescribe Refusal No products>"
                    ]
            }

            test "the message is the body then the contact, an empty contact adding nothing" {
                let n = notice english salbutamol OrderContextRefusal.NoDoseRules

                message n |> Expect.equal "both" $"{n.Body} {n.Contact}"
                message { n with Contact = "" } |> Expect.equal "the body alone" n.Body
            }
        ]
