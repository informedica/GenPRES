namespace Informedica.GenPRES.Client.Core.Tests


/// The one rule for what a pick field offers.
module PickPolicyTests =

    open Expecto
    open Expecto.Flip
    open PickPolicy


    let private pick options chosen clearable enabled =
        {
            Options = options
            Chosen = chosen
            Clearable = clearable
            Enabled = enabled
        }


    [<Tests>]
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
                            pick [| "a"; "b" |] None true false
                            |> acceptsChange
                            |> Expect.isFalse "disabled"
                        }
                    ]
            ]
