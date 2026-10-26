namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// The picks the workbench dialog keeps: what a change or a step from the dialog picked, what a
/// reopen keeps, and what the field decision reads of them.
module PickListPolicyTests =

    open Informedica.GenPRES.Client.Core.Policies

    open Expecto
    open Expecto.Flip
    open Informedica.GenPRES.Shared.Types


    [<Tests>]
    let tests =
        let vu values : ValueUnit =
            Informedica.GenPRES.Shared.Models.Order.ValueUnit.create values "mg" "Mass" true "dutch" ""

        let variable name nonZero vals : Variable =
            Informedica.GenPRES.Shared.Models.Order.Variable.create name nonZero None false None None false vals

        let ovar name nonZero vals : OrderVariable =
            {
                Name = name
                DefinedConstraints = variable name false None
                CalculatedConstraints = variable name false None
                Variable = variable name nonZero vals
                LargeIncr = None
                Level = IsNormal
            }

        let three = Some(vu [| "1", 1m; "2", 2m; "3", 3m |])
        let one = Some(vu [| "2", 2m |])

        // an order whose frequency and dose quantity the tests set; every other variable stays as the
        // empty order has it
        let order freq dose =
            // every field a default, built by reflection: the order graph is too deep to write by hand
            let rec defaultOf (t: System.Type) : obj =
                if t = typeof<string> then
                    box ""
                elif t = typeof<bool> then
                    box false
                elif t = typeof<int> then
                    box 0
                elif t = typeof<decimal> then
                    box 0m
                elif t = typeof<float> then
                    box 0.0
                elif t = typeof<System.DateTime> then
                    box System.DateTime.MinValue
                elif t.IsArray then
                    box (System.Array.CreateInstance(t.GetElementType(), 0))
                elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>> then
                    null
                elif Microsoft.FSharp.Reflection.FSharpType.IsRecord t then
                    Microsoft.FSharp.Reflection.FSharpValue.MakeRecord(
                        t,
                        Microsoft.FSharp.Reflection.FSharpType.GetRecordFields t
                        |> Array.map (fun f -> defaultOf f.PropertyType)
                    )
                elif Microsoft.FSharp.Reflection.FSharpType.IsUnion t then
                    let case = (Microsoft.FSharp.Reflection.FSharpType.GetUnionCases t)[0]
                    Microsoft.FSharp.Reflection.FSharpValue.MakeUnion(
                        case,
                        case.GetFields() |> Array.map (fun f -> defaultOf f.PropertyType)
                    )
                else
                    null

            let o = defaultOf typeof<Order> :?> Order

            { o with
                Schedule = { o.Schedule with Frequency = freq }
                Orderable = { o.Orderable with Dose = { o.Orderable.Dose with Quantity = dose } }
            }

        let before = order (ovar "frq" false three) (ovar "dos" false three)

        testList
            "PickListPolicy"
            [
                test "a variable narrowed to one value is picked; one left as it was is not" {
                    order (ovar "frq" false three) (ovar "dos" false one)
                    |> PickListPolicy.picked before
                    |> Expect.equal "the dose" [ "dos" ]
                }

                test "a variable cleared is no pick" {
                    let picked = order (ovar "frq" false one) (ovar "dos" false one)

                    order (ovar "frq" true None) (ovar "dos" false one)
                    |> PickListPolicy.picked picked
                    |> Expect.isEmpty "nothing picked"
                }

                test "a pick is added at the end, moved when picked before; unknown picks stay unknown" {
                    let after = order (ovar "frq" false three) (ovar "dos" false one)

                    Some [| "dos"; "frq" |]
                    |> PickListPolicy.afterChange before after
                    |> Expect.equal "the dose the latest" (Some [| "frq"; "dos" |])

                    None |> PickListPolicy.afterChange before after |> Expect.isNone "unknown"
                }

                test "a step that moved its variable makes it the latest pick; one at a bound does not" {
                    let stepped = order (ovar "frq" false three) (ovar "dos" false one)

                    Some [| "dos"; "frq" |]
                    |> PickListPolicy.afterStep "dos" before stepped
                    |> Expect.equal "the dose the latest" (Some [| "frq"; "dos" |])

                    Some [| "frq" |]
                    |> PickListPolicy.afterStep "dos" before before
                    |> Expect.equal "at a bound, as it was" (Some [| "frq" |])

                    None |> PickListPolicy.afterStep "dos" before stepped |> Expect.isNone "unknown"
                }

                test "an answer that is the order sent puts the picks from before the change back" {
                    let after = order (ovar "frq" false three) (ovar "dos" false one)
                    let earlier = Some [| "dos"; "frq" |]
                    let changed = Some [| "frq"; "dos" |]

                    changed
                    |> PickListPolicy.afterAnswer before before earlier
                    |> Expect.equal "not applied, as before the change" earlier

                    changed
                    |> PickListPolicy.afterAnswer before after earlier
                    |> Expect.equal "applied, as the change left them" changed

                    // a reset empties the picks before it is sent
                    Some [||]
                    |> PickListPolicy.afterAnswer before before earlier
                    |> Expect.equal "a refused reset keeps the picks" earlier
                }

                test "a reopen keeps the picks made before the reopened one" {
                    Some [| "frq"; "dos"; "rate" |]
                    |> PickListPolicy.beforeReopen "dos"
                    |> Expect.equal "the frequency" (Some [| "frq" |])

                    Some [| "frq" |]
                    |> PickListPolicy.beforeReopen "dos"
                    |> Expect.equal "not a pick, all kept" (Some [| "frq" |])

                    None |> PickListPolicy.beforeReopen "dos" |> Expect.isNone "unknown"
                }

                test "the field decision reads the picks: yes, no, unknown" {
                    PickListPolicy.constrained (Some [| "dos" |]) "dos"
                    |> Expect.equal "picked" FieldOpenPolicy.Constrained.Yes

                    PickListPolicy.constrained (Some [| "dos" |]) "frq"
                    |> Expect.equal "not picked" FieldOpenPolicy.Constrained.No

                    PickListPolicy.constrained None "frq"
                    |> Expect.equal "unknown" FieldOpenPolicy.Constrained.Unknown
                }
            ]
