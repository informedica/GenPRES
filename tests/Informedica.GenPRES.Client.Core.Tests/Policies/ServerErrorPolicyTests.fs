namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// How long a server error stays on the banner: until the next successful answer of the kind
/// of request that raised it, or until the server check succeeds.
module ServerErrorPolicyTests =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Policies

    open Expecto
    open Expecto.Flip
    open ServerErrorPolicy


    let sources =
        [
            ErrorSource.OrderPlan
            ErrorSource.Formulary
            ErrorSource.Parenteralia
            ErrorSource.Interactions
            ErrorSource.Totals
            ErrorSource.Login
            ErrorSource.LogFiles
            ErrorSource.LogAnalysis
            ErrorSource.Reload
            ErrorSource.Server
        ]


    [<Tests>]
    let tests =
        testList
            "ServerErrorPolicy"
            [
                testList
                    "raised"
                    [
                        test "keeps the source and the errors" {
                            raised ErrorSource.OrderPlan [| "x"; "y" |]
                            |> Expect.equal
                                "should hold both"
                                {
                                    Source = ErrorSource.OrderPlan
                                    Errors = [| "x"; "y" |]
                                }
                        }

                        test "four errors are cut to three, each at 200 characters" {
                            let long = String.replicate 250 "a"
                            let cut = String.replicate 200 "a"

                            (raised ErrorSource.Formulary [| long; "b"; "c"; "d" |]).Errors
                            |> Expect.equal "three, the first cut" [| $"%s{cut}..."; "b"; "c" |]
                        }
                    ]

                testList
                    "message"
                    [
                        // the Dutch rows of the terms, as the sheet has them
                        let dutch term =
                            match term with
                            | Terms.``Server Error Text`` -> "Serverfout: {0}"
                            | _ -> english term

                        test "the errors after the sentence" {
                            raised ErrorSource.OrderPlan [| "x"; "y" |]
                            |> message english
                            |> Expect.equal "should list the errors" "Server error: x; y"
                        }

                        test "in the language of the terms" {
                            raised ErrorSource.OrderPlan [| "x"; "y" |]
                            |> message dutch
                            |> Expect.equal "should be Dutch" "Serverfout: x; y"
                        }

                        test "the server check gives the sentence that it does not answer, whatever the error" {
                            raised ErrorSource.Server [| "connection refused" |]
                            |> message english
                            |> Expect.equal
                                "the sentence"
                                "The server cannot be reached. Check that the server has been started."
                        }

                        test "the English of every banner term is set" {
                            for term in
                                [
                                    Terms.``Server Error Title``
                                    Terms.``Server Error Unreachable``
                                    Terms.``Server Error Text``
                                ] do
                                english term |> Expect.notEqual $"default for {term}" $"{term}"
                        }
                    ]

                testList
                    "clearedBy"
                    [
                        for source in sources do
                            test $"a success of %A{source} clears the error it raised" {
                                Some(raised source [| "error" |])
                                |> clearedBy source
                                |> Expect.isNone "should be cleared"
                            }

                        for source in sources do
                            test $"the server check clears an error of %A{source}" {
                                Some(raised source [| "error" |])
                                |> clearedBy ErrorSource.Server
                                |> Expect.isNone "should be cleared"
                            }

                        for raiser in sources do
                            for answer in sources do
                                if raiser <> answer && answer <> ErrorSource.Server then
                                    test $"a success of %A{answer} keeps an error of %A{raiser}" {
                                        let error = Some(raised raiser [| "error" |])

                                        error |> clearedBy answer |> Expect.equal "should be kept" error
                                    }

                        test "a formulary answer keeps an order plan error" {
                            let error = Some(raised ErrorSource.OrderPlan [| "error" |])

                            error |> clearedBy ErrorSource.Formulary |> Expect.equal "should be kept" error
                        }

                        test "a log file listing keeps a failed reload" {
                            let error = Some(raised ErrorSource.Reload [| "error" |])

                            error |> clearedBy ErrorSource.LogFiles |> Expect.equal "should be kept" error
                        }

                        test "an unreachable server stays after an order plan answer" {
                            let error = Some(raised ErrorSource.Server [| "unreachable" |])

                            error |> clearedBy ErrorSource.OrderPlan |> Expect.equal "should be kept" error
                        }

                        for source in sources do
                            test $"no error stays none after %A{source}" {
                                None |> clearedBy source |> Expect.isNone "should stay none"
                            }
                    ]
            ]
