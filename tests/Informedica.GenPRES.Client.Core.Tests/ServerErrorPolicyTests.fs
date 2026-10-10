namespace Informedica.GenPRES.Client.Core.Tests


/// How long a server error stays on the banner: until the next successful answer of the kind
/// of request that raised it, or until the server check succeeds.
module ServerErrorPolicyTests =

    open Expecto
    open Expecto.Flip
    open ServerErrorPolicy


    let sources =
        [
            ErrorSource.OrderPlan
            ErrorSource.Formulary
            ErrorSource.Parenteralia
            ErrorSource.Interactions
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
                        test "keeps the source and the errors after the sentence" {
                            raised ErrorSource.OrderPlan [| "x"; "y" |]
                            |> Expect.equal
                                "should hold both"
                                {
                                    Source = ErrorSource.OrderPlan
                                    Message = "Server fout: x; y"
                                }
                        }

                        test "four errors are cut to three, each at 200 characters" {
                            let long = String.replicate 250 "a"
                            let cut = String.replicate 200 "a"

                            (raised ErrorSource.Formulary [| long; "b"; "c"; "d" |]).Message
                            |> Expect.equal "three, the first cut" $"Server fout: %s{cut}...; b; c"
                        }

                        test "the server check gives the sentence that it does not answer, whatever the error" {
                            (raised ErrorSource.Server [| "connection refused" |]).Message
                            |> Expect.equal
                                "the sentence"
                                "De server is niet bereikbaar. Controleer of de server is gestart."
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
