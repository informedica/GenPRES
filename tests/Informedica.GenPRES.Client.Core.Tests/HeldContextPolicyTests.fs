namespace Informedica.GenPRES.Client.Core.Tests


/// Whether the patient context is held, against the version last opened or signed.
module HeldContextPolicyTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types
    open HeldContextPolicy


    let context id generic =
        { Shared.Models.OrderContext.empty with
            Id = id
            OrderContext.Filter.Generic = Some generic
        }

    let plan contexts =
        Shared.Models.OrderPlan.create Shared.Models.Patient.empty contexts

    let para = context "c1" "paracetamol"
    let morf = context "c2" "morfine"
    let amox = context "c3" "amoxicilline"

    /// The contexts of the version opened.
    let opened = [| para; morf |]

    /// A context of that version with its order changed.
    let changedMorf = { morf with OrderContext.Filter.Indication = Some "pijn" }


    [<Tests>]
    let tests =
        testList
            "HeldContextPolicy"
            [
                test "the version opened is released" {
                    changed opened (plan opened) |> Expect.isEmpty "the version holds nothing"
                }

                test "an empty order plan on no version is released" {
                    changed [||] (plan [||]) |> Expect.isEmpty "nothing to hold"
                }

                test "an added order holds" {
                    changed opened (plan [| para; morf; amox |])
                    |> Expect.equal "the added context is new" [| "c3" |]
                }

                test "any order on no version holds" {
                    changed [||] (plan [| para |]) |> Expect.equal "every context is new" [| "c1" |]
                }

                test "a changed order of the version holds" {
                    changed opened (plan [| para; changedMorf |])
                    |> Expect.equal "the changed context is changed" [| "c2" |]
                }

                test "removing every new and changed order releases" {
                    changed opened (plan [| para |])
                    |> Expect.isEmpty "the order of the version left is as opened"
                }

                test "removing an order of the version alone does not hold" {
                    changed opened (plan [| morf |])
                    |> Expect.isEmpty "a removal is no new or changed order"
                }

                test "a change to the filter alone does not hold" {
                    changed opened { plan opened with Filtered = [| "c1" |] }
                    |> Expect.isEmpty "the rows shown are no order"
                }

                test "totals recalculated alone do not hold" {
                    let now =
                        { plan opened with
                            Totals = { Shared.Models.Totals.empty with Volume = [| TextItem.Normal "10 mL" |] }
                        }

                    changed opened now |> Expect.isEmpty "the totals are no order"
                }
            ]
