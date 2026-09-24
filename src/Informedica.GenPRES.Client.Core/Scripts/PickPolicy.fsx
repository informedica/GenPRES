// One rule for what a pick field offers (plan 981, step C2).
//
// The client has two rules today. The filter selects of the prescribing, formulary and
// parenteralia pages disable a field with no option but leave a field with one option enabled,
// as if there were something to choose (#498). The order fields choose a single value by
// themselves and stay enabled. And a cross that clears is shown by whoever asks, whether or
// not clearing means anything. One rule, here, decides all three from what the field is given:
// how many options, which is chosen, whether the caller allows clearing, whether the field is
// enabled at all.
//
// Script-first draft (script-only policy: Client.Core is not the client UI) of the rule, →
// `Client.Core/PickPolicy.fs`, with its tests → `Client.Core.Tests/PickPolicyTests.fs`. The
// component of step C2, `Components/PickField.fs`, renders what the rule answers.
//
// Run: `dotnet fsi PickPolicy.fsx` from this directory, or via the FSI MCP after
// `#I "<this directory>"`.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"


// ---------------------------------------------------------------------------------------------
// → Client.Core/PickPolicy.fs
// ---------------------------------------------------------------------------------------------

/// What a pick field offers, decided from what it is given rather than by each page: a field
/// with nothing to choose is disabled and empty; a field with one option shows it chosen and
/// is disabled, since there is nothing to pick; a field with more is enabled when its caller
/// says so, shows what was chosen, and clears when its caller allows and something is chosen.
/// Pure F#, no React, so it runs under Expecto.
module PickPolicy =

    /// What the field is given: its options as keys, the key chosen, whether clearing is
    /// allowed, and whether the caller has it enabled.
    type Pick =
        {
            Options: string[]
            Chosen: string option
            Clearable: bool
            Enabled: bool
        }


    /// What the field shows: whether it can be used, the key shown as chosen, and whether the
    /// cross that clears is offered.
    type Offer =
        {
            Disabled: bool
            Selected: string option
            CanClear: bool
        }


    /// The rule.
    let offer (pick: Pick) =
        match pick.Options with
        | [||] ->
            {
                Disabled = true
                Selected = None
                CanClear = false
            }
        | [| only |] ->
            {
                Disabled = true
                Selected = Some only
                CanClear = false
            }
        | options ->
            // a chosen key the options no longer hold is not shown as chosen
            let selected = pick.Chosen |> Option.filter (fun k -> options |> Array.contains k)

            {
                Disabled = not pick.Enabled
                Selected = selected
                CanClear = pick.Clearable && pick.Enabled && selected.IsSome
            }


    /// Whether a change the user makes is taken: only from a field that is offered enabled.
    let acceptsChange (pick: Pick) = (offer pick).Disabled |> not


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open PickPolicy

let pick options chosen clearable enabled =
    {
        Options = options
        Chosen = chosen
        Clearable = clearable
        Enabled = enabled
    }

let tests =
    testList
        "PickPolicy"
        [
            testList
                "no option"
                [
                    test "is disabled, empty, without a cross, whatever else is asked" {
                        for clearable in [ true; false ] do
                            for enabled in [ true; false ] do
                                pick [||] (Some "a") clearable enabled
                                |> offer
                                |> Expect.equal
                                    $"clearable {clearable}, enabled {enabled}"
                                    {
                                        Disabled = true
                                        Selected = None
                                        CanClear = false
                                    }
                    }

                    test "takes no change" {
                        pick [||] None true true |> acceptsChange |> Expect.isFalse "nothing to pick"
                    }
                ]

            testList
                "one option"
                [
                    test "shows it chosen and is disabled, since there is nothing to pick" {
                        pick [| "only" |] None true true
                        |> offer
                        |> Expect.equal
                            "the one option, chosen"
                            {
                                Disabled = true
                                Selected = Some "only"
                                CanClear = false
                            }
                    }

                    test "shows it chosen also when something else was chosen before" {
                        (pick [| "only" |] (Some "gone") true true |> offer).Selected
                        |> Expect.equal "the one option" (Some "only")
                    }

                    test "offers no cross, whatever the caller allows" {
                        (pick [| "only" |] (Some "only") true true |> offer).CanClear
                        |> Expect.isFalse "clearing the only option would choose it again"
                    }
                ]

            testList
                "more options"
                [
                    test "is enabled when the caller has it enabled" {
                        (pick [| "a"; "b" |] None false true |> offer).Disabled
                        |> Expect.isFalse "enabled"

                        (pick [| "a"; "b" |] None false false |> offer).Disabled
                        |> Expect.isTrue "disabled by the caller"
                    }

                    test "shows what was chosen when the options hold it" {
                        (pick [| "a"; "b" |] (Some "b") false true |> offer).Selected
                        |> Expect.equal "b" (Some "b")
                    }

                    test "shows nothing chosen when the options no longer hold it" {
                        (pick [| "a"; "b" |] (Some "c") false true |> offer).Selected
                        |> Expect.equal "not among the options" None
                    }

                    test "offers the cross only when allowed, enabled and something is chosen" {
                        [
                            (pick [| "a"; "b" |] (Some "a") true true, true)
                            (pick [| "a"; "b" |] None true true, false)
                            (pick [| "a"; "b" |] (Some "a") false true, false)
                            (pick [| "a"; "b" |] (Some "a") true false, false)
                        ]
                        |> List.map (fun (p, expected) -> (offer p).CanClear = expected)
                        |> List.forall id
                        |> Expect.isTrue "each case as expected"
                    }

                    test "takes a change when enabled, not when disabled" {
                        pick [| "a"; "b" |] None true true |> acceptsChange |> Expect.isTrue "enabled"
                        pick [| "a"; "b" |] None true false |> acceptsChange |> Expect.isFalse "disabled"
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
