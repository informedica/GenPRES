/// The client's copies of the order plan rules agree with the domain: for any contract model
/// plan, each display projection in `Shared.Models` answers what the domain's rule answers over
/// the plan parsed at the boundary. The copies exist so that the pages can read the plan they
/// hold; this test is what keeps them from drifting.
module Informedica.GenPRES.Server.Tests.AgreementTests

open Expecto
open Expecto.Flip
open FsCheck
open Informedica.GenOrder.Lib
// after Expecto, whose FocusState has a Normal case too
open Shared.Types
open ServerApi
open Informedica.GenPRES.Server.Tests.StubAdapterTests


module Domain = Informedica.GenOrder.Lib.OrderPlan
module DomainContext = Informedica.GenOrder.Lib.PlanContext


let categories =
    [
        NutritionCategory.EnteralFeeding
        NutritionCategory.EnteralSupplement
        NutritionCategory.TPN
        NutritionCategory.Lipid
        NutritionCategory.ElectrolyteGlucose
    ]


/// The shape of a plan: per context its category and how many scenarios it holds (none, one
/// or two, so that it contributes an order or not), and whether the row filter names it. A
/// filter that names no context is the empty filter, which the contract model reads as
/// keeping every context, so a plan whose rows are all hidden cannot be stated and is not
/// generated; the shape with every context unnamed is the plan without a filter, and both
/// copies have to agree on that reading too.
type Shape = { Contexts: (OrderCategory * int * bool) list }


type Generators =
    static member Shape() =
        gen {
            let! n = Gen.choose (0, 4)

            let! contexts =
                Gen.listOfLength
                    n
                    (gen {
                        let! category =
                            Gen.elements (OrderCategory.Drug :: (categories |> List.map OrderCategory.Nutrition))

                        let! scenarios = Gen.choose (0, 2)
                        let! kept = Arb.generate<bool>
                        return category, scenarios, kept
                    })

            return { Contexts = contexts }
        }
        |> Arb.fromGen


let config =
    { FsCheckConfig.defaultConfig with
        maxTest = 60
        arbitrary = [ typeof<Generators> ]
    }


/// The contract model plan of a shape: contexts `c-0`, `c-1`, ... on the stub patient, each
/// scenario a paracetamol order with an id of its own, the filter naming the kept ones.
let planOf (shape: Shape) : OrderPlan =
    let contexts =
        shape.Contexts
        |> List.mapi (fun i (category, scenarios, _) ->
            { Shared.Models.OrderContext.empty with
                Id = $"c-{i}"
                Category = category
                Patient = StubPatientData.patient
                Scenarios = Array.init scenarios (fun k -> SessionStubTests.scenarioWithOrder $"o-{i}-{k}")
            }
        )
        |> List.toArray

    { Shared.Models.OrderPlan.create StubPatientData.patient contexts with
        Filtered =
            shape.Contexts
            |> List.mapi (fun i (_, _, kept) -> if kept then Some $"c-{i}" else None)
            |> List.choose id
            |> List.toArray
    }


let orderId (sc: Types.OrderScenario) =
    let (Id id) = sc.Order.Id
    id


[<Tests>]
let tests =
    testList
        "the client's copies agree with the domain"
        [
            testPropertyWithConfig
                config
                "orders, filtered and nutritionContexts"
                (fun (shape: Shape) ->
                    let plan = planOf shape
                    let parsed = SessionStubTests.parsed plan

                    let orders =
                        Shared.Models.OrderPlan.orders plan |> Array.map _.Order.Id =
                            (Domain.orders parsed |> Array.map orderId)

                    let filtered =
                        Shared.Models.OrderPlan.filtered plan |> Array.map _.Id =
                            (Domain.filtered parsed |> Array.map _.Id)

                    let nutrition =
                        Shared.Models.OrderPlan.nutritionContexts plan |> Array.map _.Id =
                            (Domain.nutritionContexts parsed |> Array.map _.Id)

                    orders && filtered && nutrition
                )

            testPropertyWithConfig
                config
                "contribution and nutritionCategory, context by context"
                (fun (shape: Shape) ->
                    let plan = planOf shape
                    let parsed = SessionStubTests.parsed plan

                    Array.zip plan.OrderContexts parsed.Contexts
                    |> Array.forall (fun (ctx, pc) ->
                        let contribution =
                            Shared.Models.OrderContext.contribution ctx |> Option.map _.Order.Id =
                                (DomainContext.contribution pc |> Option.map orderId)

                        let category =
                            Shared.Models.OrderContext.nutritionCategory ctx
                            |> Option.map OrderCategory.Nutrition
                                =
                                (DomainContext.nutritionCategory pc
                                 |> Option.map (Types.OrderCategory.Nutrition >> OrderContextMapper.Category.ofDomain))

                        contribution && category
                    )
                )

            testPropertyWithConfig
                config
                "mayAdd says yes exactly where admits does"
                (fun (shape: Shape) ->
                    let plan = planOf shape
                    let parsed = SessionStubTests.parsed plan

                    categories
                    |> List.forall (fun category ->
                        Shared.Models.OrderPlan.mayAdd category plan =
                            (parsed
                             |> Domain.admits (OrderPlanMapper.nutritionCategory category)
                             |> Result.isOk)
                    )
                )
        ]
