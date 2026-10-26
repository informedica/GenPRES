// Step 4 of the plan for #985, the server's half: the outcome of GenORDER's evaluation reaches
// the wire as the contract's OrderContextResponse. Three pieces are prototyped here, the rest
// of the step being the retyping of the port and the command handler:
//
// - Filter.dropped (→ GenORDER, Dtos.fs, beside Filter.reconcile): whether the reconciliation
//   let a pick go, which it does silently when the rules no longer offer it for the patient.
//   That is how a seed from the emergency list for a patient outside every rule lands today:
//   the pick vanishes and the page shows the whole list, which is the dead end of #911 and
//   #478 in its current form.
// - PlanContext.evaluateOutcome (→ GenORDER, OrderPlan.fs, beside evaluate): the plan context
//   reconciled, and refused with the context as it was sent when a pick was dropped; else the
//   command run as an outcome and the intake recorded over the answer, a refusal there again
//   carrying the context as sent.
// - OrderContextService.evaluateOutcome (→ ServerApi.Services.fs): the same over the provider,
//   an exception the refusal of the error channel.
// - OrderContextService.toResponse (→ ServerApi.Services.fs): the outcome mapped out as the
//   contract's response, GenORDER's refusal as the contract's.
//
// The plan lane's Navigate and the nutrition discovery keep the message-list evaluate, whose
// refusal is the Dutch message it has always been; a refusal there can only follow a rules
// reload, since the plan holds only contexts that evaluated once.
//
// Run: `dotnet fsi OrderContextOutcome.fsx` from this directory, after `dotnet run build`.

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "nuget: Expecto"

open Expecto
open Expecto.Flip

open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open ServerApi


module Picks =

    /// Whether the reconciliation let a pick go: the held filter chose what the reconciled one
    /// no longer does, for any of the five picks.
    let dropped (held: Filter) (reconciled: Filter) =
        let gone (chosen: 'a option) (kept: 'a option) = chosen.IsSome && kept.IsNone

        gone held.Indication reconciled.Indication
        || gone held.Generic reconciled.Generic
        || gone held.Route reconciled.Route
        || gone held.Form reconciled.Form
        || gone held.DoseType reconciled.DoseType


module PlanContext =

    open Informedica.GenOrder.Lib.PlanContext


    /// The plan context evaluated against the rules as an outcome. Reconciled first: for the two
    /// commands that look the rules up, a pick the rules no longer offer for the patient is a
    /// refusal with the context as it was sent, not a pick dropped in silence. Else the command
    /// run, and the intake recorded over the answer, evaluated or refused. The id and the category stay the plan's. An evaluation that fails
    /// is the answer.
    let evaluateOutcome
        (start: System.DateTime)
        logger
        provider
        (totalsData: Types.Data.TotalsData[])
        (cmd: OrderContext -> OrderContext.Command)
        (pc: PlanContext)
        : Result<Outcome<PlanContext>, Message list>
        =
        let sent = pc.Context
        let reconciled = sent |> OrderContext.reconcile logger provider

        match reconciled |> cmd with
        // the two commands that look the rules up: a pick dropped is their refusal
        | OrderContext.UpdateOrderContext _
        | OrderContext.ReloadResources _ when Picks.dropped sent.Filter reconciled.Filter ->
            Refused(pc, OrderContext.refusal provider sent) |> Ok
        | command ->
            command
            |> OrderContext.evaluateOutcome start logger provider
            |> Result.map (
                Outcome.map (fun answer ->
                    let ctx = answer |> OrderContext.Command.get

                    { pc with
                        Context = ctx
                        Intake = ctx |> OrderContext.intake totalsData
                    }
                )
            )


module OrderContextService =

    open Informedica.GenPRES.Shared.Types
    open ServerApi.OrderContextService


    /// The plan context evaluated against the rules, as an outcome: the domain's pipeline, the
    /// intake over the provider's totals data. An exception on the way is the refusal of the
    /// error channel.
    let evaluateOutcome
        (start: System.DateTime)
        logger
        (provider: Resources.IResourceProvider)
        (cmd: Informedica.GenOrder.Lib.Types.OrderContext -> OrderContext.Command)
        (pc: PlanContext)
        : Result<Outcome<PlanContext>, string[]>
        =
        try
            pc
            |> PlanContext.evaluateOutcome start logger provider (provider.GetTotals()) cmd
            |> Result.mapError refusal
        with e ->
            Logging.ServerLogging.Error $"errored:\n{e}"
            |> Informedica.Logging.Lib.Logging.logError logger

            Error [| e.Message |]


    /// GenORDER's refusal as the contract's.
    let refusalToModel =
        function
        | Refusal.NoDoseRules -> OrderContextRefusal.NoDoseRules
        | Refusal.NoDoseRulesForPatient -> OrderContextRefusal.NoDoseRulesForPatient
        | Refusal.NoProducts -> OrderContextRefusal.NoProducts


    /// The outcome mapped out as the contract's response, with the environment's demo flag.
    let toResponse (demo: bool) (outcome: Outcome<PlanContext>) : OrderContextResponse =
        let model (pc: PlanContext) = pc |> PlanContext.Dto.toDto |> OrderContextMapper.toModel demo

        match outcome with
        | Evaluated pc -> OrderContextResponse.Evaluated(model pc)
        | Refused(pc, refusal) -> OrderContextResponse.Refused(model pc, refusalToModel refusal)


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

/// A provider holding no rules: what the rule lookup reads answers empty, every other resource
/// raises. The same as the GenORDER tests' NoRules.
type NoRules() =
    interface Resources.IResourceProvider with
        member _.Get(_: Resources.ResourceKey<'T>) : 'T = raise (System.NotImplementedException())

        member _.GetData() = raise (System.NotImplementedException())
        member _.GetUnitMappings() = raise (System.NotImplementedException())
        member _.GetRouteMappings() = [||]
        member _.GetValidForms() = raise (System.NotImplementedException())
        member _.GetFormRoutes() = raise (System.NotImplementedException())
        member _.GetFormularyProducts() = raise (System.NotImplementedException())
        member _.GetReconstitution() = raise (System.NotImplementedException())
        member _.GetParenteralMeds() = raise (System.NotImplementedException())
        member _.GetEnteralFeeding() = raise (System.NotImplementedException())
        member _.GetProducts() = raise (System.NotImplementedException())
        member _.GetDoseRules() = [||]
        member _.GetSolutionRules() = [||]
        member _.GetRenalRules() = [||]
        member _.GetTotals() = [||]
        member _.GetGStandProvider() = raise (System.NotImplementedException())
        member _.GetDepartments() = Resources.Departments.ofNamed []
        member _.GetResourceInfo() = raise (System.NotImplementedException())


let start = System.DateTime(2026, 9, 28)
let logger = OrderLogging.noOp
let provider = NoRules()


/// The switch tests' context: a plan context by id and category, a pick made.
let ctx: Informedica.GenPRES.Shared.Types.OrderContext =
    { Informedica.GenPRES.Shared.Models.OrderContext.empty with
        Id = "c-1"
        Category = Informedica.GenPRES.Shared.Types.OrderCategory.Drug
        DemoVersion = true
        Filter = { Informedica.GenPRES.Shared.Models.OrderContext.filter with Generic = Some "glucose" }
        Patient = StubPatientData.patient
    }


let parsed (ctx: Informedica.GenPRES.Shared.Types.OrderContext) =
    match ctx |> ServerApi.OrderContextService.parse with
    | Ok pc -> pc
    | Error e -> failtest $"no plan context: %A{e}"


let evaluate cmd pc = pc |> OrderContextService.evaluateOutcome start logger provider cmd


let tests =
    testList
        "the outcome on the server"
        [
            test "a pick without rules is refused with the plan context as sent, id and category kept" {
                match ctx |> parsed |> evaluate OrderContext.UpdateOrderContext with
                | Ok(Refused(pc, Refusal.NoDoseRules)) ->
                    pc.Id |> Expect.equal "the id" "c-1"
                    pc.Category |> Expect.equal "the category" Types.OrderCategory.Drug
                    pc.Context.Filter.Generic |> Expect.equal "the pick kept" (Some "glucose")
                | other -> failtest $"expected a refusal without rules, got %A{other}"
            }

            test "no pick is evaluated" {
                let fresh = { ctx with Filter = Informedica.GenPRES.Shared.Models.OrderContext.filter }

                match fresh |> parsed |> evaluate OrderContext.UpdateOrderContext with
                | Ok(Evaluated pc) -> pc.Id |> Expect.equal "the id" "c-1"
                | other -> failtest $"expected an evaluation, got %A{other}"
            }

            test "a scenario command is evaluated" {
                match ctx |> parsed |> evaluate OrderContext.SelectOrderScenario with
                | Ok(Evaluated _) -> ()
                | other -> failtest $"expected an evaluation, got %A{other}"
            }

            test "the response carries the context as sent, the refusal in the contract's words, the demo flag the environment's" {
                match ctx |> parsed |> evaluate OrderContext.UpdateOrderContext with
                | Ok outcome ->
                    match outcome |> OrderContextService.toResponse false with
                    | Informedica.GenPRES.Shared.Types.OrderContextResponse.Refused(model, Informedica.GenPRES.Shared.Types.OrderContextRefusal.NoDoseRules) ->
                        model.Id |> Expect.equal "the id" "c-1"
                        model.Filter.Generic |> Expect.equal "the pick kept" (Some "glucose")
                        model.DemoVersion |> Expect.isFalse "the environment's demo flag"
                    | other -> failtest $"expected the refusal, got %A{other}"
                | Error e -> failtest $"failed: %A{e}"
            }

            test "the three refusals map one to one" {
                [ Refusal.NoDoseRules; Refusal.NoDoseRulesForPatient; Refusal.NoProducts ]
                |> List.map OrderContextService.refusalToModel
                |> Expect.equal
                    "in order"
                    [
                        Informedica.GenPRES.Shared.Types.OrderContextRefusal.NoDoseRules
                        Informedica.GenPRES.Shared.Types.OrderContextRefusal.NoDoseRulesForPatient
                        Informedica.GenPRES.Shared.Types.OrderContextRefusal.NoProducts
                    ]
            }

            test "a pick the rules do not offer is dropped, not refused" {
                let fresh = { ctx with Filter = Informedica.GenPRES.Shared.Models.OrderContext.filter }
                let held = (parsed ctx).Context.Filter
                let reconciled = (parsed fresh).Context.Filter

                Picks.dropped held reconciled |> Expect.isTrue "the generic gone"
                Picks.dropped held held |> Expect.isFalse "kept"
                Picks.dropped reconciled held |> Expect.isFalse "nothing chosen, nothing gone"
            }

            test "the message-list evaluate of the plan lane still drops the pick in silence: the follow-up" {
                match
                    ctx
                    |> parsed
                    |> ServerApi.OrderContextService.evaluate start logger provider OrderContext.UpdateOrderContext
                with
                | Ok pc -> pc.Context.Filter.Generic |> Expect.isNone "the pick gone, nothing said"
                | Error e -> failtest $"failed: %A{e}"
            }
        ]


runTestsWithCLIArgs [] [||] tests
