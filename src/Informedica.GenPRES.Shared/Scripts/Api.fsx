// The signed record stores the contexts (plan 667, step 6): a signed version holds every order
// context of the plan as it was at the signature, its order inside, instead of the scenarios
// alone. A reopen is then the plan as it was: `PlanCommand.Open` hands the contexts back with
// nothing evaluated, the pick lists, the candidates and the stepped values included. The
// signing challenge compares the contexts as it compares the orders. Closes #666.
//
// Script-first draft (script-only policy) of what goes to `Shared/Types.fs` and `Shared/Api.fs`;
// the server half is in `Server/Scripts/Plan.fsx`.
//
// Run: `dotnet fsi Api.fsx` from this directory.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"
#r "nuget: Fable.Remoting.Json, 3.0"

#load "../Types.fs"
#load "../Calculations.fs"
#load "../Utils.fs"
#load "../Localization.fs"
#load "../Models.fs"
#load "../Api.fs"

open Shared.Types


/// → `Shared/Types.fs`, in place of `SignedOrderPlan`.
type SignedOrderPlan667 =
    {
        Head: OrderPlanHead
        PatientId: string
        Base: string option
        // every context of the plan as signed: a reopen is the plan as it was
        OrderContexts: OrderContext[]
        Patient: Patient
        Verified: bool
    }


/// → `Shared/Api.fs`, one more `PlanCommand` case and its log name.
[<RequireQualifiedAccess>]
type PlanCommand667 =
    // the signed version as it was, nothing evaluated: the contexts as given, their orders
    // derived; the patient with no contexts is the empty plan
    | Open of Patient * OrderContext[]


module PlanCommand667 =

    let toString cmd =
        match cmd with
        | PlanCommand667.Open(_, contexts) -> $"Open %i{contexts.Length}"


open Expecto
open Expecto.Flip
open Newtonsoft.Json
open Fable.Remoting.Json
open Shared.Models


let converters = [| FableJsonConverter() :> JsonConverter |]
let toJson (v: 'a) = JsonConvert.SerializeObject(v, converters)
let ofJson<'a> (json: string) = JsonConvert.DeserializeObject<'a>(json, converters)


let tests =
    testList
        "the signed record stores the contexts"
        [
            test "a version with two contexts round-trips on the wire, contexts and all" {
                let signed: SignedOrderPlan667 =
                    {
                        Head =
                            {
                                Id = "plan-1"
                                No = 1
                                By =
                                    {
                                        UserId = "u"
                                        DisplayName = "Stub Prescriber"
                                        Role = UserRole.Prescriber
                                    }
                                SignedAt = System.DateTime(2026, 9, 14, 12, 0, 0, System.DateTimeKind.Utc)
                            }
                        PatientId = "p"
                        Base = None
                        OrderContexts =
                            [|
                                { OrderContext.empty with Id = "c-1" }
                                { OrderContext.empty with
                                    Id = "c-2"
                                    Category = OrderCategory.Nutrition NutritionCategory.TPN
                                }
                            |]
                        Patient = Patient.empty
                        Verified = true
                    }

                signed |> toJson |> ofJson<SignedOrderPlan667> |> Expect.equal "the same version back" signed
            }

            test "the log names the count, never the contexts" {
                PlanCommand667.Open(Patient.empty, [| OrderContext.empty; OrderContext.empty |])
                |> PlanCommand667.toString
                |> Expect.equal "the count" "Open 2"
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
