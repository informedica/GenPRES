namespace Informedica.GenPRES.Client.Core.Tests


/// Text built from translated terms.
module TermTextTests =

    open Expecto
    open Expecto.Flip


    [<Tests>]
    let tests =
        testList
            "TermText"
            [
                testList
                    "fill"
                    [
                        test "fills the values in order" {
                            "{0} of {1}"
                            |> TermText.fill [ "2"; "3" ]
                            |> Expect.equal "the values in their places" "2 of 3"
                        }

                        test "fills a placeholder that appears twice both times" {
                            "{0} and {0}" |> TermText.fill [ "a" ] |> Expect.equal "both filled" "a and a"
                        }

                        test "leaves a placeholder without a value as it is" {
                            "{0} of {1}"
                            |> TermText.fill [ "2" ]
                            |> Expect.equal "the second left" "2 of {1}"
                        }

                        test "leaves a text without placeholders as it is" {
                            "no values" |> TermText.fill [ "2" ] |> Expect.equal "unchanged" "no values"
                        }
                    ]

                testList
                    "sentences"
                    [
                        test "joins the sentences with a space" {
                            [ "One."; "Two." ] |> TermText.sentences |> Expect.equal "one body" "One. Two."
                        }

                        test "drops an empty translation" {
                            [ "One."; ""; "Two." ]
                            |> TermText.sentences
                            |> Expect.equal "no double space" "One. Two."
                        }

                        test "no sentences make an empty body" { [] |> TermText.sentences |> Expect.equal "empty" "" }
                    ]
            ]
