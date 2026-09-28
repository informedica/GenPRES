// Step 5 of the plan for #985: the words of a refusal. The server says why no dose can be shown
// as one of three cases; the page renders a notice from the terms of #1148: a title, the body
// of the case with the picks filled in, and the contact sentence. Pure F#, the pattern of
// SessionGatePolicy, so it runs under Expecto; Views/Prescribe.fs renders it.
//
// The module below is `OrderContextRefusalPolicy.fs` as it becomes, after SessionGatePolicy.fs
// in the project; the tests at the end migrate to `OrderContextRefusalPolicyTests.fs`.
//
// Run: `dotnet fsi OrderContextRefusalPolicy.fsx` from this directory, after `dotnet run build`.

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "nuget: Expecto"

open Expecto
open Expecto.Flip


/// The notice of a refused order context: what the prescribing page says when no dose can be
/// shown, from the refusal the server sent and the picks the user made. Pure F#, no React, so
/// it runs under Expecto; Views/Prescribe.fs renders it. The texts are `Terms`, translated by
/// the caller: the view passes the sheet lookup, the tests pass `english`.
module OrderContextRefusalPolicy =

    open Shared
    open Shared.Types
    open Shared.Models


    /// What the page shows for a refusal: the title, the body naming the picks, and whom to
    /// tell.
    type Notice =
        {
            Title: string
            Body: string
            Contact: string
        }


    /// The English of the refusal terms: what the notice shows when the sheet has no row for a
    /// term, or the terms have not loaded. Any other term falls back to its name.
    let english (term: Terms) =
        match term with
        | Terms.``Prescribe Refusal`` -> "No dose can be shown"
        | Terms.``Prescribe Refusal No dose rules`` -> "There is no dose rule for {0}"
        | Terms.``Prescribe Refusal Patient`` ->
            "There are dose rules for {0}, but none covers the age, weight or department of this patient"
        | Terms.``Prescribe Refusal No products`` ->
            "There are dose rules for {0} that cover this patient, but none has a product that can be prescribed"
        | Terms.``Prescribe Refusal Contact`` ->
            "Report this to the pharmacy or the application manager, so that the rule can be added"
        | term -> $"{term}"


    /// The picks the notice names, in the order the page shows them: indication, medication,
    /// route, form and dose type, as far as chosen, one after the other.
    let picks (filter: Filter) =
        [
            filter.Indication
            filter.Generic
            filter.Route
            filter.Form
            filter.DoseType |> Option.map DoseType.doseTypeToDescription
        ]
        |> List.choose id
        |> String.concat ", "


    /// The body's term per case.
    let bodyTerm (refusal: OrderContextRefusal) =
        match refusal with
        | OrderContextRefusal.NoDoseRules -> Terms.``Prescribe Refusal No dose rules``
        | OrderContextRefusal.NoDoseRulesForPatient -> Terms.``Prescribe Refusal Patient``
        | OrderContextRefusal.NoProducts -> Terms.``Prescribe Refusal No products``


    /// The notice for the context refused: the title, the body of the case with the picks
    /// filled in, and the contact sentence.
    let notice (tr: Terms -> string) (ctx: OrderContext) (refusal: OrderContextRefusal) : Notice =
        {
            Title = tr Terms.``Prescribe Refusal``
            Body = tr (bodyTerm refusal) |> SessionGatePolicy.fill [ picks ctx.Filter ]
            Contact = tr Terms.``Prescribe Refusal Contact``
        }


    /// The notice's text under its title: the body, then whom to tell; an empty translation
    /// adds no sentence.
    let message (notice: Notice) = SessionGatePolicy.sentences [ notice.Body; notice.Contact ]


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

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
                    (english (bodyTerm refusal)).Contains "{0}" |> Expect.isTrue $"{refusal} names the picks"

                (english Terms.``Prescribe Refusal``).Contains "{0}" |> Expect.isFalse "the title takes none"
                (english Terms.``Prescribe Refusal Contact``).Contains "{0}" |> Expect.isFalse "the contact takes none"
            }

            test "the picks are named in the page's order, as far as chosen" {
                picks salbutamol.Filter |> Expect.equal "generic, route" "salbutamol, intraveneus"

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


runTestsWithCLIArgs [] [||] tests
