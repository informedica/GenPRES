namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// The one rule for what a pick field offers.
module PickPolicyTests =

    open Informedica.GenPRES.Client.Core.Policies

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


    let forms = [| "tablet"; "capsule"; "solution" |]

    let form = Informedica.GenPRES.Shared.Models.OrderContext.Form


    /// The forms the offer of hydrocortison at 13:00 lists, as the field sends a pick in them.
    [<Tests>]
    let filterCommandTests =
        testList
            "PickPolicy.filterCommand"
            [
                test "the hydrocortison capsule picked is the form at position 1" {
                    filterCommand form forms (Some "capsule")
                    |> Expect.equal
                        "the second form"
                        (Some(Informedica.GenPRES.Shared.Api.OrderViewCommand.SetNthFilterProperty(form, 1)))
                }

                test "the form emptied clears the form" {
                    filterCommand form forms None
                    |> Expect.equal
                        "a clear"
                        (Some(Informedica.GenPRES.Shared.Api.OrderViewCommand.ClearFilterProperty form))
                }

                test "the NaCl 0.9% diluent picked is the diluent at position 1, emptied a clear" {
                    let diluents = [| "glucose 5%"; "NaCl 0.9%" |]

                    let diluentCommand n =
                        match n with
                        | Some n -> Informedica.GenPRES.Shared.Api.OrderViewCommand.SetNthDiluentProperty n
                        | None -> Informedica.GenPRES.Shared.Api.OrderViewCommand.ClearDiluentProperty

                    command diluentCommand diluents (Some "NaCl 0.9%")
                    |> Expect.equal
                        "the second diluent"
                        (Some(Informedica.GenPRES.Shared.Api.OrderViewCommand.SetNthDiluentProperty 1))

                    command diluentCommand diluents None
                    |> Expect.equal
                        "a clear"
                        (Some Informedica.GenPRES.Shared.Api.OrderViewCommand.ClearDiluentProperty)

                    command diluentCommand diluents (Some "water") |> Expect.isNone "nothing"
                }

                test "a form the list does not hold sends nothing" {
                    position forms (Some "suppository")
                    |> Expect.equal "not offered" Position.NotOffered
                    filterCommand form forms (Some "suppository") |> Expect.isNone "nothing"
                }
            ]
