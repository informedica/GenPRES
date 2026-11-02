/// What the tests of an order on new patient data share.
module DataUpdateFixtures

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib


let logger = OrderLogging.noOp

let kg v = v |> ValueUnit.singleWithUnit Units.Weight.kiloGram

let cm v = v |> ValueUnit.singleWithUnit Units.Height.centiMeter

let patient w =
    OrderPlanTests.RuleFixtures.patient (Some 1N) false (Some w) None

/// A solved order, made when a test asks for it.
let order = lazy (Scenarios.cotrim |> Tests.TestOrder.processed)

/// A scenario of the order, with what the order context needs to read it.
let scenario ord =
    { OrderPlanTests.Fixtures.scenario "o" with
        Order = ord
        Form = "zetpil"
        Components = [| "cotrimoxazol" |]
        UseAdjust = true
    }

/// The context on new patient data.
let onData provider pat (ctx: OrderContext) =
    match ctx |> OrderContext.orderOnPatientData logger provider pat ctx.Scenarios[0] with
    | Evaluated after -> after
    | Refused _ -> Expecto.Tests.failtest "refused"
