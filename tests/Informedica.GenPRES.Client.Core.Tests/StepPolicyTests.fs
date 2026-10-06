namespace Informedica.GenPRES.Client.Core.Tests


/// What a field's arrows send: one command for the net clicks of one pair.
module StepPolicyTests =

    open Expecto
    open Expecto.Flip
    open StepPolicy


    [<Tests>]
    let tests =
        testList
            "StepPolicy"
            [
                test "the clicks of one pair add up to one net count" {
                    [
                        // up twice, down once: one step up
                        net (1 + 1 - 1) 0
                        // up, then down before the field sends: nothing
                        net (1 - 1) 0
                        net 0 -3
                        net 0 0
                    ]
                    |> Expect.equal
                        "one up, nothing, three large down, nothing"
                        [ Some(Pair.Inner, 1); None; Some(Pair.Outer, -3); None ]
                }

                test "a pair rests while the other holds clicks" {
                    [
                        rests Pair.Inner 0 0
                        rests Pair.Outer 0 0
                        rests Pair.Outer 2 0
                        rests Pair.Inner 0 -1
                        rests Pair.Inner 2 0
                    ]
                    |> Expect.equal "only the other pair's clicks rest a pair" [ false; false; true; true; false ]
                }
            ]
