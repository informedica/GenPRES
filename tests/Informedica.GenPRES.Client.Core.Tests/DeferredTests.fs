namespace Informedica.GenPRES.Client.Core.Tests


/// The reading of a plain fetch, linked in from the client project.
module DeferredTests =

    open Expecto
    open Expecto.Flip


    let cases: (string * Deferred<int>) list =
        [
            "not started", HasNotStartedYet
            "in progress", InProgress
            "resolved", Resolved 1
            "refreshing", Refreshing 1
        ]


    [<Tests>]
    let tests =
        testList
            "Deferred"
            [
                test "map carries the value of an answer and of one kept while the fetch runs again" {
                    Resolved 1 |> Deferred.map ((+) 1) |> Expect.equal "resolved" (Resolved 2)
                    Refreshing 1 |> Deferred.map ((+) 1) |> Expect.equal "refreshing" (Refreshing 2)
                    InProgress |> Deferred.map ((+) 1) |> Expect.equal "in progress" InProgress
                    HasNotStartedYet
                    |> Deferred.map ((+) 1)
                    |> Expect.equal "not started" HasNotStartedYet
                }

                test "bind keeps a value kept: an answer of the function over it is refreshing too" {
                    Resolved 1
                    |> Deferred.bind (fun n -> Resolved(n + 1))
                    |> Expect.equal "resolved" (Resolved 2)

                    Refreshing 1
                    |> Deferred.bind (fun n -> Resolved(n + 1))
                    |> Expect.equal "refreshing stays refreshing" (Refreshing 2)

                    Refreshing 1
                    |> Deferred.bind (fun _ -> InProgress)
                    |> Expect.equal "the function's other cases as they are" InProgress

                    InProgress
                    |> Deferred.bind (fun n -> Resolved(n + 1))
                    |> Expect.equal "in progress" InProgress
                }

                test "defaultValue and toOption read the value of an answer and of one kept" {
                    for name, deferred in cases do
                        let expected =
                            match deferred with
                            | Resolved v
                            | Refreshing v -> Some v
                            | _ -> None

                        deferred |> Deferred.toOption |> Expect.equal name expected

                        deferred
                        |> Deferred.defaultValue 0
                        |> Expect.equal name (expected |> Option.defaultValue 0)
                }

                test "refresh keeps the value when there is one, and shows nothing otherwise" {
                    Resolved 1
                    |> Deferred.refresh
                    |> Expect.equal "an answer is kept" (Refreshing 1)
                    Refreshing 1
                    |> Deferred.refresh
                    |> Expect.equal "a value kept stays kept" (Refreshing 1)
                    InProgress |> Deferred.refresh |> Expect.equal "nothing to keep" InProgress
                    HasNotStartedYet
                    |> Deferred.refresh
                    |> Expect.equal "not asked yet: asked" InProgress
                }
            ]
