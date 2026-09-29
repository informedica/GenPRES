namespace Informedica.GenPRES.Client.Core.Tests


/// The picks the client keeps: what a change from the dialog picked, and what the field decision
/// reads of them.
module PickListTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types


    [<Tests>]
    let tests =
        let vu values : ValueUnit =
            Shared.Models.Order.ValueUnit.create values "mg" "Mass" true "dutch" ""

        let variable name nonZero vals : Variable =
            Shared.Models.Order.Variable.create name nonZero None false None None false vals

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
            "PickList"
            [
                test "a variable narrowed to one value is picked; one left as it was is not" {
                    order (ovar "frq" false three) (ovar "dos" false one)
                    |> PickList.picked before
                    |> Expect.equal "the dose" [ "dos" ]
                }

                test "a variable cleared is no pick" {
                    let picked = order (ovar "frq" false one) (ovar "dos" false one)

                    order (ovar "frq" true None) (ovar "dos" false one)
                    |> PickList.picked picked
                    |> Expect.isEmpty "nothing picked"
                }

                test "a pick is added at the end, moved when picked before; unknown picks stay unknown" {
                    let after = order (ovar "frq" false three) (ovar "dos" false one)

                    Some [| "dos"; "frq" |]
                    |> PickList.afterChange before after
                    |> Expect.equal "the dose the latest" (Some [| "frq"; "dos" |])

                    None |> PickList.afterChange before after |> Expect.isNone "unknown"
                }

                test "the field decision reads the picks: yes, no, unknown" {
                    PickList.constrained (Some [| "dos" |]) "dos"
                    |> Expect.equal "picked" FieldOpenPolicy.Constrained.Yes

                    PickList.constrained (Some [| "dos" |]) "frq"
                    |> Expect.equal "not picked" FieldOpenPolicy.Constrained.No

                    PickList.constrained None "frq"
                    |> Expect.equal "unknown" FieldOpenPolicy.Constrained.Unknown
                }
            ]
