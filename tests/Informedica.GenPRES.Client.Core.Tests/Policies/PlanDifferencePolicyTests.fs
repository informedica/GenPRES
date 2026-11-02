namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// The differences of the order plan against the version last opened or signed, as the sign dialog
/// lists them.
module PlanDifferencePolicyTests =

    open Informedica.GenPRES.Client.Core.Policies

    open Expecto
    open Expecto.Flip
    open Informedica.GenPRES.Shared.Types
    open PlanDifferencePolicy
    open PlanContextPolicyTests


    let kinds (ds: (OrderContext * Difference)[]) = ds |> Array.map (fun (c, d) -> c.Id, d)


    [<Tests>]
    let differenceTests =
        testList
            "PlanDifferencePolicy.differences"
            [
                test "an unchanged plan has no differences" {
                    differences opened (plan opened)
                    |> Expect.isEmpty "nothing new, changed or removed"
                }

                test "a new, a changed and a removed context, in plan order then removed" {
                    differences opened (plan [| amox; para |> withFrequency 3m |])
                    |> kinds
                    |> Expect.equal
                        "new and changed in plan order, then removed"
                        [| "c3", Difference.New; "c1", Difference.Changed; "c2", Difference.Removed |]
                }

                test "a removed row carries the opened context" {
                    differences opened (plan [| para |])
                    |> Array.map fst
                    |> Expect.equal "the context as opened" [| morf |]
                }

                test "on no version every context is new" {
                    differences [||] (plan opened)
                    |> kinds
                    |> Expect.equal "all new, in plan order" [| "c1", Difference.New; "c2", Difference.New |]
                }

                test "a new context that contributes no order is left out" {
                    differences opened (plan [| para; morf; tpn |])
                    |> Expect.isEmpty "nothing to list yet"
                }

                test "a context of the version that contributed no order is left out" {
                    differences [| para; morf; tpn |] (plan [| para; morf |])
                    |> Expect.isEmpty "it was never listed, so it is not removed"
                }

                test "a context of the version narrowed since is new" {
                    let narrowed = { tpn with Scenarios = [| scenario "o-c4" "tpn" |] }

                    differences [| para; tpn |] (plan [| para; narrowed |])
                    |> kinds
                    |> Expect.equal "its order enters the plan now" [| "c4", Difference.New |]
                }

                test "a filter difference alone is no difference" {
                    let filtered = { para with OrderContext.Filter.Indication = Some "pijn" }

                    differences opened (plan [| filtered; morf |])
                    |> Expect.isEmpty "the difference rule sees the order"
                }

                test "another medication in the same context is a change" {
                    differences opened (plan [| para |> withOrder (fun o -> { o with Id = "o-other" }); morf |])
                    |> kinds
                    |> Expect.equal "the context is changed" [| "c1", Difference.Changed |]
                }

                test "the argumentation alone is a change" {
                    differences opened (plan [| { para with Argumentation = Some "sepsis" }; morf |])
                    |> kinds
                    |> Expect.equal "the context is changed" [| "c1", Difference.Changed |]
                }

                test "a change of the plan's filtered rows or totals alone is no change" {
                    let now =
                        { plan opened with
                            Filtered = [| "c1" |]
                            Totals =
                                { Informedica.GenPRES.Shared.Models.Totals.empty with
                                    Volume = [| TextItem.Normal "10 mL" |]
                                }
                        }

                    differences opened now
                    |> Expect.isEmpty "the rows shown and the totals are no order"
                }
            ]
