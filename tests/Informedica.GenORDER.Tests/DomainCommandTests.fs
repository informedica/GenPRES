/// The order context commands for the client's specific commands: each changes the context and
/// answers as today's command on the changed context, over the test scenarios. No rules are loaded.
module DomainCommandTests

open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip

open OrderPlanTests.EvaluateFixtures

module Variable = Informedica.GenSolver.Lib.Variable


let start = System.DateTime(2026, 10, 5)


let evaluate cmd =
    cmd
    |> OrderContext.evaluateOutcome start OrderLogging.noOp (OrderPlanTests.NoRules())


/// The context of a fixture: the held filter and one scenario with the order.
let contextOf ord =
    { pcmContext with Scenarios = [| { pcmScenario with Order = ord } |] }


/// The order variables of the one scenario of an evaluated context.
let variablesOf outcome =
    match outcome with
    | Ok(Evaluated cmd) ->
        (cmd |> OrderContext.Command.get).Scenarios
        |> Array.tryExactlyOne
        |> Option.map (_.Order >> Order.toOrdVars)
    | _ -> None


/// The filter of the context a refusal sent back, the context as the command changed it.
let refusedFilter outcome =
    match outcome with
    | Ok(Refused(cmd, _)) -> Some (cmd |> OrderContext.Command.get).Filter
    | _ -> None


[<Tests>]
let tests =
    testList
        "the specific commands in the domain"
        [
            test "a filter pick looks the rules up for the changed context" {
                OrderContext.ChangeFilter(pcmContext, OrderCategory.Drug, FilterField.Indication, Some 0)
                |> evaluate
                |> refusedFilter
                |> Option.bind _.Indication
                |> Expect.equal "the first indication sent" (Some pcmContext.Filter.Indications[0])
            }

            test "a filter pick answers as today's update of the changed context" {
                let changed =
                    pcmContext
                    |> OrderContext.changeFilter OrderCategory.Drug FilterField.Generic (Some 0)
                    |> Result.defaultWith failtest

                OrderContext.ChangeFilter(pcmContext, OrderCategory.Drug, FilterField.Generic, Some 0)
                |> evaluate
                |> refusedFilter
                |> Expect.equal
                    "the same filter"
                    (changed |> OrderContext.UpdateOrderContext |> evaluate |> refusedFilter)
            }

            test "a specific command answers as itself" {
                match OrderContext.ClearAllFilter pcmContext |> evaluate with
                | Ok(Evaluated(OrderContext.ClearAllFilter _))
                | Ok(Refused(OrderContext.ClearAllFilter _, _)) -> ()
                | other -> failtest $"%A{other}"
            }

            test "a filter pick in the plan reads its index in the lists as sent, not as reconciled" {
                // the rules offer none of the sent indications, so a reconcile first would leave
                // nothing at index 0
                let pc = PlanContext.create "c-1" OrderCategory.Drug pcmContext

                pc
                |> PlanContext.evaluateOutcome
                    start
                    OrderLogging.noOp
                    (OrderPlanTests.NoRules())
                    (fun () -> [||])
                    (fun ctx -> OrderContext.ChangeFilter(ctx, OrderCategory.Drug, FilterField.Indication, Some 0))
                |> function
                    | Ok(Evaluated pc)
                    | Ok(Refused(pc, _)) ->
                        pc.Context.Filter.Indication
                        |> Expect.equal "the first indication sent" (Some pcmContext.Filter.Indications[0])
                    | Error e -> failtest $"%A{e}"
            }

            test "a filter index past the options is an error" {
                OrderContext.ChangeFilter(pcmContext, OrderCategory.Drug, FilterField.Route, Some 99)
                |> evaluate
                |> Result.isError
                |> Expect.isTrue "an error"
            }

            test "a clear of the whole filter keeps the patient" {
                match OrderContext.ClearAllFilter pcmContext |> evaluate with
                | Ok(Evaluated cmd)
                | Ok(Refused(cmd, _)) ->
                    let ctx = cmd |> OrderContext.Command.get
                    ctx.Filter |> Expect.equal "the empty filter" OrderContext.emptyFilter
                    ctx.Patient |> Expect.equal "the patient kept" pcmContext.Patient
                | other -> failtest $"%A{other}"
            }

            test "a scenario index past the scenarios is an error" {
                OrderContext.SelectNthOrderScenario(pcmContext, 1)
                |> evaluate
                |> Result.isError
                |> Expect.isTrue "an error"
            }

            test "a value pick gives the order of today's update with the variable set" {
                [
                    for name, ord in OrderValueCommandTests.fixtures.Value do
                        for f in OrderValueCommandTests.fields ord do
                            match ord |> f.Get with
                            | Some ovar when ovar.Variable |> Variable.count > 1 ->
                                let ctx = contextOf ord
                                let set = ord |> OrderValueCommandTests.change (f.SetNth 0)

                                let expected =
                                    { ctx with Scenarios = [| { ctx.Scenarios[0] with Order = set } |] }
                                    |> OrderContext.UpdateOrderScenario
                                    |> evaluate
                                    |> variablesOf

                                let actual = OrderContext.SetNthOrderValue(ctx, f.SetNth 0) |> evaluate |> variablesOf

                                if actual <> expected then
                                    $"%s{name} %s{f.Name}"
                            | _ -> ()
                ]
                |> fun msgs -> msgs |> Expect.isEmpty $"the same order: %A{msgs}"
            }

            test "a value clear gives the order of today's reopen with the variable cleared" {
                [
                    for name, ord in OrderValueCommandTests.fixtures.Value do
                        let ctx = contextOf ord
                        // every variable a pick, so each clear reopens with the picks before it
                        let picks =
                            OrderValueCommandTests.fields ord
                            |> List.choose (fun f -> f.Get ord)
                            |> List.map OrderProcessor.nameOf

                        for f in OrderValueCommandTests.fields ord do
                            match ord |> f.Get with
                            | Some ovar when ovar.Variable |> Variable.count > 1 ->
                                let cleared = ord |> OrderValueCommandTests.change f.Clear

                                let expected =
                                    ({ ctx with Scenarios = [| { ctx.Scenarios[0] with Order = cleared } |] }, picks)
                                    |> OrderContext.ReopenOrderScenario
                                    |> evaluate
                                    |> variablesOf

                                let actual =
                                    OrderContext.ClearOrderValue(ctx, f.Clear, picks) |> evaluate |> variablesOf

                                if actual <> expected then
                                    $"%s{name} %s{f.Name}"
                            | _ -> ()
                ]
                |> fun msgs -> msgs |> Expect.isEmpty $"the same order: %A{msgs}"
            }
        ]
