namespace Informedica.GenPRES.Client.Core.Tests.Helpers


/// The English the alert terms fall back to when the sheet has no row for a term.
module AlertTests =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Helpers

    open Expecto
    open Expecto.Flip


    [<Tests>]
    let tests =
        testList
            "Alert"
            [
                testList
                    "english"
                    [
                        test "the English of every alert term is set" {
                            for term in
                                [
                                    Terms.``Alert Interaction Names Not Loaded``
                                    Terms.``Alert Interactions Found``
                                    Terms.``Alert Close Failed``
                                    Terms.``Alert Pin Not Sent``
                                    Terms.``Alert Request Failed``
                                    Terms.``Alert Something Went Wrong``
                                ] do
                                Alert.english term |> Expect.notEqual $"default for {term}" $"{term}"
                        }

                        // the English of this term is its name, so it is checked by value
                        test "the English of Invalid password is set" {
                            Alert.english Terms.``Invalid password``
                            |> Expect.equal "the English" "Invalid password"
                        }

                        test "the number of interactions is filled in" {
                            Alert.english Terms.``Alert Interactions Found``
                            |> TermText.fill [ "2" ]
                            |> Expect.equal "should hold the number" "2 interaction(s) found."
                        }
                    ]
            ]
