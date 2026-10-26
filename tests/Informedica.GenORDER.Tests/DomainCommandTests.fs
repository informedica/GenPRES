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


/// The order of each value command scenario as the dialog first shows it, built once, when a test
/// first asks for it.
let builtScenarios =
    OrderValueCommandTests.scenarios
    |> List.map (fun (name, med) -> name, lazy (med |> OrderValueCommandTests.built))
    |> Map.ofList


/// The fields of a value command scenario. The fields follow the names of the components and items,
/// so the order before any calculation gives them.
let fieldsOf med =
    med
    |> Medication.toOrder Scenarios.testStart
    |> Result.map OrderValueCommandTests.fields


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

            for name, med in OrderValueCommandTests.scenarios do
                match med |> fieldsOf with
                | Error e -> test $"the %s{name} order builds" { failtest $"could not build the order: %A{e}" }
                | Ok fields ->
                    for field in fields do
                        test
                            $"a value pick gives the order of today's update with the variable set: %s{name} %s{field.Name}" {
                            let ord = builtScenarios[name].Value

                            match ord |> field.Get with
                            | Some ovar when ovar.Variable |> Variable.count > 1 ->
                                let ctx = contextOf ord
                                let set = ord |> OrderValueCommandTests.change (field.SetNth 0)

                                let expected =
                                    { ctx with Scenarios = [| { ctx.Scenarios[0] with Order = set } |] }
                                    |> OrderContext.UpdateOrderScenario
                                    |> evaluate
                                    |> variablesOf

                                OrderContext.SetNthOrderValue(ctx, field.SetNth 0)
                                |> evaluate
                                |> variablesOf
                                |> Expect.equal "the same order" expected
                            | _ -> ()
                        }

                        test
                            $"a value clear gives the order of today's reopen with the variable cleared: %s{name} %s{field.Name}" {
                            let ord = builtScenarios[name].Value

                            match ord |> field.Get with
                            | Some ovar when ovar.Variable |> Variable.count > 1 ->
                                let ctx = contextOf ord
                                // every variable a pick, so the clear reopens with the picks before it
                                let picks =
                                    OrderValueCommandTests.fields ord
                                    |> List.choose (fun f -> f.Get ord)
                                    |> List.map OrderProcessor.nameOf

                                let cleared = ord |> OrderValueCommandTests.change field.Clear

                                let expected =
                                    ({ ctx with Scenarios = [| { ctx.Scenarios[0] with Order = cleared } |] }, picks)
                                    |> OrderContext.ReopenOrderScenario
                                    |> evaluate
                                    |> variablesOf

                                OrderContext.ReCalcOrderValue(ctx, field.Clear, picks)
                                |> evaluate
                                |> variablesOf
                                |> Expect.equal "the same order" expected
                            | _ -> ()
                        }
        ]


/// The plan's answer to a command over a plan context holding this context.
let inPlan ctx cmd =
    PlanContext.create "c-1" OrderCategory.Drug ctx
    |> PlanContext.evaluateOutcome start OrderLogging.noOp (OrderPlanTests.NoRules()) (fun () -> [||]) cmd
    |> Result.map (
        function
        | Evaluated pc -> pc.Context, None
        | Refused(pc, r) -> pc.Context, Some r
    )
    |> Result.toOption


/// An order that cannot be solved: the full medication through the stages before the solve, each
/// carrying its order on as the application does.
let unsolvable =
    lazy
        let run cmd ord =
            match ord |> cmd |> OrderProcessor.processPipeline OrderLogging.noOp with
            | Ok o
            | Error(o, _) -> o

        match Scenarios.fullMedication |> Medication.toOrder start with
        | Ok ord -> ord |> run CalcMinMax |> run IncreaseIncrements |> run CalcValues
        | Error e -> failtest $"could not build the order: %A{e}"


[<Tests>]
let notSolvedTests =
    testList
        "an order that cannot be solved"
        [
            test "a solve that fails refuses the context as sent" {
                let sent = contextOf unsolvable.Value

                match sent |> OrderContext.UpdateOrderScenario |> evaluate with
                | Ok(Refused(OrderContext.UpdateOrderScenario ctx, Refusal.NotSolved)) ->
                    ctx |> Expect.equal "the context as sent" sent
                | other -> failtest $"expected a refusal, got %A{other}"
            }

            test "a value pick that cannot be solved refuses the context before the pick" {
                let sent = contextOf unsolvable.Value
                let pick = OrderContext.SetNthOrderValue(sent, SetNthOrderableDoseQuantity 0)

                match pick |> evaluate with
                | Ok(Refused(OrderContext.SetNthOrderValue(ctx, _), Refusal.NotSolved)) ->
                    ctx |> Expect.equal "the context before the pick" sent
                | other -> failtest $"expected a refusal, got %A{other}"
            }

            test "the message-list evaluate answers the context before the change" {
                let sent = contextOf unsolvable.Value
                let evaluate = OrderContext.evaluate start OrderLogging.noOp (OrderPlanTests.NoRules())

                match sent |> OrderContext.UpdateOrderScenario |> evaluate with
                | Ok(OrderContext.UpdateOrderScenario ctx) -> ctx |> Expect.equal "the context as sent" sent
                | other -> failtest $"expected the context, got %A{other}"

                // until now the pick came back set and unsolved
                match OrderContext.SetNthOrderValue(sent, SetNthOrderableDoseQuantity 0) |> evaluate with
                | Ok(OrderContext.SetNthOrderValue(ctx, _)) -> ctx |> Expect.equal "the context before the pick" sent
                | other -> failtest $"expected the context, got %A{other}"
            }
        ]


[<Tests>]
let seedTests =
    let seed source ind gen rte frm dt = pcmContext |> OrderContext.seedFilter source ind gen rte frm dt

    let held = pcmContext.Filter

    testList
        "the seeds and the patient change"
        [
            test "a formulary page with the same choices keeps the diluent and the components" {
                let ctx = seed SeedSource.Formulary held.Indication held.Generic held.Route held.Form held.DoseType

                ctx.Filter.Diluent |> Expect.equal "the diluent kept" held.Diluent
                ctx.Filter.SelectedComponents
                |> Expect.equal "the components kept" held.SelectedComponents
                ctx.Scenarios |> Expect.isEmpty "the scenarios go"
            }

            test "a formulary page with another indication lets go of the diluent and the components" {
                let ctx = seed SeedSource.Formulary (Some "pijn") held.Generic held.Route held.Form held.DoseType

                ctx.Filter.Indication |> Expect.equal "the indication written" (Some "pijn")
                ctx.Filter.Diluent |> Expect.isNone "the diluent goes"
                ctx.Filter.SelectedComponents |> Expect.isEmpty "the components go"
            }

            test "a parenteralia page does not compare the indication" {
                let ctx = seed SeedSource.Parenteralia None held.Generic held.Route held.Form None

                ctx.Filter.Indication |> Expect.isNone "the indication cleared"
                ctx.Filter.DoseType |> Expect.isNone "the dose type cleared"
                ctx.Filter.Diluent |> Expect.equal "the diluent kept" held.Diluent
                ctx.Scenarios |> Expect.isEmpty "the scenarios go"
            }

            test "a parenteralia page with another route lets go of the diluent and the components" {
                let ctx = seed SeedSource.Parenteralia None held.Generic (Some "rect") held.Form None

                ctx.Filter.Diluent |> Expect.isNone "the diluent goes"
                ctx.Filter.SelectedComponents |> Expect.isEmpty "the components go"
            }

            test "the url's medication is written over the filter as it is" {
                let ctx = seed SeedSource.Url (Some "pijn") (Some "ibuprofen") None None None

                ctx.Filter.Generic |> Expect.equal "the medication written" (Some "ibuprofen")
                ctx.Filter.Route |> Expect.isNone "the route written"
                ctx.Filter.Diluent |> Expect.equal "the diluent kept" held.Diluent
                ctx.Scenarios |> Expect.equal "the scenarios kept" pcmContext.Scenarios
            }

            test "a list item is written over an empty filter, the patient kept" {
                let ctx = seed SeedSource.MedicationList None (Some "ibuprofen") (Some "or") None None

                ctx.Filter
                |> Expect.equal
                    "the item's choices alone"
                    { OrderContext.emptyFilter with
                        Generic = Some "ibuprofen"
                        Route = Some "or"
                    }

                ctx.Scenarios |> Expect.isEmpty "no scenarios"
                ctx.Patient |> Expect.equal "the patient kept" pcmContext.Patient
            }

            test "a reload changes nothing" {
                seed SeedSource.Reload None None None None None
                |> Expect.equal "the context as it is" pcmContext
            }

            test "a seed answers in the plan as the update of the context it seeds" {
                [
                    SeedSource.Url
                    SeedSource.MedicationList
                    SeedSource.Formulary
                    SeedSource.Parenteralia
                    SeedSource.Reload
                ]
                |> List.filter (fun source ->
                    let seeded = seed source (Some "pijn") (Some "ibuprofen") (Some "or") None None

                    // today the client sends the context it seeded as an update
                    inPlan
                        pcmContext
                        (fun ctx ->
                            OrderContext.SeedFilter(ctx, source, Some "pijn", Some "ibuprofen", Some "or", None, None)
                        )
                    <> inPlan seeded OrderContext.UpdateOrderContext
                )
                |> Expect.isEmpty "the same answer for every source"
            }

            test "a patient change answers in the plan as the update of the context with the patient" {
                let pat = { pcmContext.Patient with Department = Some "NEO" }

                // today the client sends the context with the patient as an update
                inPlan pcmContext (fun ctx -> OrderContext.UpdatePatient(ctx, pat))
                |> Expect.equal
                    "the same answer"
                    (inPlan { pcmContext with Patient = pat } OrderContext.UpdateOrderContext)
            }
        ]
