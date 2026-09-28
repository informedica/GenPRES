// Migrated to Types.fs and Api.fs on 2026-09-28; kept as the prototype and as the check
// against the live provider, which the test project has no access to.
//
// Step 2 of the plan for #985: an evaluation that finds no dose rule answers a typed refusal
// instead of a message the client has to match on. Prototype of the change to the OrderContext
// module of Api.fs: the Refusal and Outcome types, the detection of which refusal it is, the
// outcome-answering getScenarios and evaluate, and the wrapper that keeps today's message for
// the callers of the message-list contract (the MCP host, the server until step 4).
//
// The tests at the end run against the provider the .env names, so they need the sheet or its
// cache; the pure ones (the picks, the refusal over a rule set) migrate as they are, the ones
// over the provider migrate with a dose-rule fixture or stay a script check.

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "../../Informedica.ZForm.Lib/bin/Debug/net10.0/Informedica.ZForm.Lib.dll"
#r "nuget: Expecto"

open System

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


/// Why an evaluation answers no scenarios: the picks of the filter match no dose rule.
[<RequireQualifiedAccess>]
type Refusal =
    /// No dose rule exists for the picks at all.
    | NoDoseRules
    /// Dose rules exist for the picks, and none of them covers this patient.
    | NoDoseRulesForPatient


/// What an evaluation answers when it does not fail: the value evaluated, or the value as it
/// was sent together with the reason it could not be evaluated.
type Outcome<'a> =
    | Evaluated of 'a
    | Refused of 'a * Refusal


module Outcome =

    let map f =
        function
        | Evaluated a -> Evaluated(f a)
        | Refused(a, r) -> Refused(f a, r)


    let get =
        function
        | Evaluated a
        | Refused(a, _) -> a


module OrderContext =

    open Informedica.GenOrder.Lib.OrderContext
    open Informedica.GenOrder.Lib.OrderContext.Helpers


    /// The message a refusal has been until now, kept for the message-list contract.
    let noDoseRulesMessage = "Geen doseerregels gevonden voor het geselecteerde filter"


    /// The five picks of the filter as the rule lookup reads them: the field's own choice,
    /// else the one option the field offers. Over no patient, so the dose rule filter leaves
    /// the patient out.
    let picks (ctx: OrderContext) : DoseFilter =
        let pick chosen offered =
            if chosen |> Option.isSome then
                chosen
            else
                offered |> Array.someIfOne

        { Filter.doseFilter with
            Indication = pick ctx.Filter.Indication ctx.Filter.Indications
            Generic = pick ctx.Filter.Generic ctx.Filter.Generics
            Route = pick ctx.Filter.Route ctx.Filter.Routes
            Form = pick ctx.Filter.Form ctx.Filter.Forms
            DoseType = pick ctx.Filter.DoseType ctx.Filter.DoseTypes
        }


    /// Which refusal an empty answer is: the dose rules for the picks, the patient left out,
    /// decide. None at all is the first case; some, none of which matched with the patient
    /// in, is the second.
    let refusalOf (rulesForPicks: Types.DoseRule[]) =
        if rulesForPicks |> Array.isEmpty then
            Refusal.NoDoseRules
        else
            Refusal.NoDoseRulesForPatient


    /// The refusal for the context, read from the provider's dose rules.
    let refusal provider (ctx: OrderContext) =
        Api.getDoseRules provider
        |> Api.filterDoseRules provider (picks ctx)
        |> refusalOf


    /// The scenarios for the context, as an outcome: evaluated, or refused with the context
    /// as it was sent, so the picks that matched nothing stay the user's. The rule lookup's
    /// own failure stays an error.
    let getScenariosOutcome
        (start: DateTime)
        logger
        provider
        (sent: OrderContext)
        : Result<Outcome<OrderContext>, Message list>
        =
        let inputFilter = sent.Filter
        let ctx, result = sent |> getRules logger provider

        let inputHadSelections =
            inputFilter.Generic.IsSome
            || inputFilter.Indication.IsSome
            || inputFilter.Route.IsSome
            || inputFilter.DoseType.IsSome

        let outputIsEmpty =
            ctx.Filter.Generics |> Array.isEmpty
            && ctx.Filter.Indications |> Array.isEmpty

        match result with
        | Error e when inputHadSelections && outputIsEmpty ->
            // propagate the underlying error when getRules failed
            Error e
        | _ when inputHadSelections && outputIsEmpty -> Refused(sent, refusal provider sent) |> Ok
        | _ ->
            let prs =
                match result with
                | Ok prs -> prs
                | Error _ -> [||]

            if prs |> Array.isEmpty then
                ctx
            else
                { ctx with
                    Scenarios =
                        // Note: different prescription rules can exist based on multiple pharmaceutical forms
                        // and multiple solution rules
                        prs
                        |> evaluateRules start logger
                        |> function
                            | [||] ->
                                // no valid results so evaluate again
                                // with changed product divisibility
                                prs |> Array.map changeRuleProductsDivisible |> evaluateRules start logger
                            | results -> results
                        |> processEvaluationResults
                        |> filterScenariosByPreparation
                }
            |> updateFilterIfOneScenario
            |> Evaluated
            |> Ok


    /// The command evaluated, as an outcome. The two commands that look the rules up can be
    /// refused; every other command is evaluated as it is today.
    let evaluateOutcome (start: DateTime) logger provider cmd : Result<Outcome<Command>, Message list> =
        match cmd with
        | UpdateOrderContext ctx ->
            ctx
            |> getScenariosOutcome start logger provider
            |> Result.map (Outcome.map UpdateOrderContext)
        | ReloadResources ctx ->
            Api.reloadCache logger provider

            ctx
            |> getScenariosOutcome start logger provider
            |> Result.map (Outcome.map ReloadResources)
        | cmd -> cmd |> evaluate start logger provider |> Result.map Evaluated


    /// The evaluate of the message-list contract, over the outcome: a refusal is the message
    /// it has always been. Keeps the MCP host and the tests as they are until they read the
    /// outcome.
    let evaluate (start: DateTime) logger provider cmd : Result<Command, Message list> =
        cmd
        |> evaluateOutcome start logger provider
        |> Result.bind (
            function
            | Evaluated cmd -> Ok cmd
            | Refused _ -> Error [ ErrorMsg(noDoseRulesMessage, None) ]
        )


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId OrderLogging.noOp (Environment.GetEnvironmentVariable "GENPRES_URL_ID")


let logger = OrderLogging.noOp
let start = DateTime(2026, 9, 28)


/// A provider that answers the dose rules given and nothing else the refusal reads.
type Rules(rules: Types.DoseRule[]) =
    interface Resources.IResourceProvider with
        member _.Get(_: Resources.ResourceKey<'T>) : 'T = raise (NotImplementedException())
        member _.GetData() = raise (NotImplementedException())
        member _.GetUnitMappings() = raise (NotImplementedException())
        member _.GetRouteMappings() = [||]
        member _.GetValidForms() = raise (NotImplementedException())
        member _.GetFormRoutes() = raise (NotImplementedException())
        member _.GetFormularyProducts() = raise (NotImplementedException())
        member _.GetReconstitution() = raise (NotImplementedException())
        member _.GetParenteralMeds() = raise (NotImplementedException())
        member _.GetEnteralFeeding() = raise (NotImplementedException())
        member _.GetProducts() = raise (NotImplementedException())
        member _.GetDoseRules() = rules
        member _.GetSolutionRules() = [||]
        member _.GetRenalRules() = [||]
        member _.GetTotals() = raise (NotImplementedException())
        member _.GetGStandProvider() = raise (NotImplementedException())
        member _.GetResourceInfo() = raise (NotImplementedException())


/// A patient of six days, the case of #911, and a child of ten years.
let sixDays =
    Patient.newBorn
    |> Patient.setAge [ 6 |> Patient.Optics.Days ]


let child = Patient.child


let contextFor pat = OrderContext.create logger provider pat


let picking generic route ind (ctx: OrderContext) =
    { ctx with
        Filter =
            { ctx.Filter with
                Generic = generic
                Route = route
                Indication = ind
            }
    }


let update ctx = Informedica.GenOrder.Lib.OrderContext.UpdateOrderContext ctx


let sentOf cmd = Informedica.GenOrder.Lib.OrderContext.Command.get cmd


let evaluateUpdate ctx =
    update ctx |> OrderContext.evaluateOutcome start logger provider


let salbutamolRules =
    Api.getDoseRules provider
    |> Api.filterDoseRules provider { Filter.doseFilter with Generic = Some "salbutamol" }


let tests =
    testList
        "a refused evaluation"
        [
            testList
                "the picks"
                [
                    test "the field's own choice is the pick" {
                        let ctx = child |> contextFor |> picking (Some "paracetamol") None None
                        (OrderContext.picks ctx).Generic |> Expect.equal "chosen" (Some "paracetamol")
                        (OrderContext.picks ctx).Patient |> Expect.equal "no patient" Patient.patient
                    }

                    test "one option offered is the pick, more are none" {
                        let ctx = child |> contextFor

                        let one =
                            { ctx with Filter = { ctx.Filter with Routes = [| "ORAAL" |] } }

                        (OrderContext.picks one).Route |> Expect.equal "the one route" (Some "ORAAL")
                        (OrderContext.picks ctx).Route |> Expect.isNone "many routes, no pick"
                    }
                ]

            testList
                "the refusal over a rule set"
                [
                    test "no rules for the picks is the first case" {
                        OrderContext.refusalOf [||] |> Expect.equal "none" Refusal.NoDoseRules
                    }

                    test "rules for the picks that left the patient out is the second case" {
                        salbutamolRules |> Array.isEmpty |> Expect.isFalse "the data holds salbutamol rules"

                        OrderContext.refusalOf salbutamolRules
                        |> Expect.equal "some" Refusal.NoDoseRulesForPatient
                    }

                    test "the refusal reads the provider's rules through the picks" {
                        let ctx = sixDays |> contextFor |> picking (Some "salbutamol") None None

                        OrderContext.refusal (Rules [||]) ctx
                        |> Expect.equal "no rules at all" Refusal.NoDoseRules

                        OrderContext.refusal (Rules salbutamolRules) ctx
                        |> Expect.equal "rules for salbutamol" Refusal.NoDoseRulesForPatient

                        { ctx with Filter = { ctx.Filter with Generic = Some "geen middel" } }
                        |> OrderContext.refusal (Rules salbutamolRules)
                        |> Expect.equal "rules for another generic only" Refusal.NoDoseRules
                    }
                ]

            testList
                "the outcome over the provider"
                [
                    test "an unknown generic is refused, no dose rules" {
                        let ctx = child |> contextFor |> picking (Some "geen middel") None None

                        match evaluateUpdate ctx with
                        | Ok(Refused(cmd, Refusal.NoDoseRules)) ->
                            (sentOf cmd).Filter.Generic |> Expect.equal "the pick kept" (Some "geen middel")
                        | other -> failtest $"expected a refusal without rules, got %A{other}"
                    }

                    test "a generic and route with rules only for another indication is refused, no dose rules" {
                        let ctx =
                            child
                            |> contextFor
                            |> picking (Some "salbutamol") (Some "INTRAVENEUS") (Some "pijn")

                        match evaluateUpdate ctx with
                        | Ok(Refused(_, Refusal.NoDoseRules)) -> ()
                        | other -> failtest $"expected a refusal without rules, got %A{other}"
                    }

                    test "salbutamol for a six-day-old on the infusion pump is refused for the patient" {
                        let ctx =
                            sixDays
                            |> contextFor
                            |> picking (Some "salbutamol") (Some "INTRAVENEUS") None

                        match evaluateUpdate ctx with
                        | Ok(Refused(cmd, Refusal.NoDoseRulesForPatient)) ->
                            let sent = sentOf cmd
                            sent.Filter.Generic |> Expect.equal "the generic kept" (Some "salbutamol")
                            sent.Filter.Route |> Expect.equal "the route kept" (Some "INTRAVENEUS")
                        | other -> failtest $"expected a refusal for the patient, got %A{other}"
                    }

                    test "a normal pick is evaluated, the lists narrowed to it" {
                        let ctx = child |> contextFor |> picking (Some "paracetamol") (Some "ORAAL") None

                        match evaluateUpdate ctx with
                        | Ok(Evaluated(Informedica.GenOrder.Lib.OrderContext.UpdateOrderContext ctx)) ->
                            ctx.Filter.Generic |> Expect.equal "the generic" (Some "paracetamol")
                            ctx.Filter.Indications |> Array.isEmpty |> Expect.isFalse "indications offered"
                        | other -> failtest $"expected an evaluation, got %A{other}"
                    }

                    test "no pick at all is evaluated, not refused" {
                        match child |> contextFor |> evaluateUpdate with
                        | Ok(Evaluated _) -> ()
                        | other -> failtest $"expected an evaluation, got %A{other}"
                    }

                    test "the message-list evaluate still answers the message" {
                        let ctx = child |> contextFor |> picking (Some "geen middel") None None

                        update ctx
                        |> OrderContext.evaluate start logger provider
                        |> Expect.equal "the message" (Error [ ErrorMsg(OrderContext.noDoseRulesMessage, None) ])
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
