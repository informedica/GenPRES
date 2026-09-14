/// The prescribing workbench as the client holds it: an order context not yet in the plan, from
/// no patient to a context shown, changed and cleared, as a pure state machine next to the
/// order plan's, with effects for the App to interpret. The machine holds the context and the
/// one request in flight; the interpreter completes each call from the open Session, keeps the
/// formulary and parenteralia filters in step, and puts words on the snackbar.
///
/// Four invariants: one request is in flight at a time, a command sent while one is under way
/// is dropped (the page greys its controls meanwhile); an answer names the request it answers
/// and lands only on that request; the workbench is always evaluated for the patient held, a
/// patient change re-evaluating it; and a filter that arrives before a patient (from the url or
/// the menu) waits as a seed and is evaluated once the patient is set.
module OrderContextMachine

open Shared.Types
open Shared.Api


/// The workbench, one phase at a time.
[<RequireQualifiedAccess>]
type OrderContextState =
    | NoPatient
    // a filter chosen before a patient is set: evaluated once one is
    | Seeded of OrderContext
    // the first evaluation for the patient, under a request id
    | Loading of Patient * request: string
    | Shown of OrderContext
    // a command in flight: the context the request found and the request id
    | Recalculating of OrderContext * request: string


/// What moves the workbench. Every message that starts a request carries the request id, minted
/// at dispatch, so that the answer can name it.
[<RequireQualifiedAccess>]
type OrderContextMsg =
    // the patient set, changed or cleared; the workbench is evaluated for it
    | PatientChanged of Patient option * request: string
    // a filter from the url or the menu, evaluated for the patient held or kept until there is one
    | Seed of OrderContext * request: string
    // an order-context command from the page, over the context as the page holds it
    | Command of OrderContextCommand * OrderContext * request: string
    // the server's answer to the request named; Error = a refusal or a transport failure
    | Answered of request: string * Result<OrderContext, string[]>
    // the workbench cleared and evaluated empty: after an order was prescribed
    | Reset of request: string


/// What the machine asks the App to do.
[<RequireQualifiedAccess>]
type OrderContextEffect =
    | CallContext of OrderContextCommand * OrderContext * request: string
    // the filter chosen, into the formulary's and the parenteralia's
    | SyncFormulary of Filter
    | SyncParenteralia of Filter
    // the server found no dose rules for the filter: back to the first page
    | GoToLifeSupport
    | TellError of string[]


module OrderContextState =

    /// The patient the workbench is evaluated for, none without one.
    let patient (state: OrderContextState) =
        match state with
        | OrderContextState.NoPatient
        | OrderContextState.Seeded _ -> None
        | OrderContextState.Loading(pat, _) -> Some pat
        | OrderContextState.Shown ctx
        | OrderContextState.Recalculating(ctx, _) -> Some ctx.Patient


    /// The workbench emptied for the patient.
    let emptyFor (pat: Patient) =
        Shared.Models.OrderContext.empty |> Shared.Models.OrderContext.setPatient pat


    /// What the server says when the filter matches no dose rule; the page then starts over.
    let noDoseRules (errs: string[]) =
        errs |> Array.exists (fun e -> e.ToLower().Contains "geen doseerregels")


    /// An evaluation of the context for the patient held: the filter into the formulary and the
    /// parenteralia as well, since the three pages share it.
    let private evaluate (ctx: OrderContext) (request: string) =
        OrderContextState.Recalculating(ctx, request),
        [
            OrderContextEffect.CallContext(OrderContextCommand.UpdateOrderContext, ctx, request)
            OrderContextEffect.SyncFormulary ctx.Filter
            OrderContextEffect.SyncParenteralia ctx.Filter
        ]


    let transition (msg: OrderContextMsg) (state: OrderContextState) : OrderContextState * OrderContextEffect list =
        match msg, state with
        // no patient, no workbench; a seed keeps waiting; whatever was in flight answers to nothing
        | OrderContextMsg.PatientChanged(None, _), OrderContextState.Seeded _ -> state, []
        | OrderContextMsg.PatientChanged(None, _), _ -> OrderContextState.NoPatient, []

        // the first patient: the empty workbench evaluated, or the seed that waited for it
        | OrderContextMsg.PatientChanged(Some pat, request), OrderContextState.NoPatient
        | OrderContextMsg.PatientChanged(Some pat, request), OrderContextState.Loading _ ->
            OrderContextState.Loading(pat, request),
            [
                OrderContextEffect.CallContext(OrderContextCommand.UpdateOrderContext, emptyFor pat, request)
            ]
        | OrderContextMsg.PatientChanged(Some pat, request), OrderContextState.Seeded ctx ->
            evaluate { ctx with Patient = pat } request

        // the patient changed: the workbench keeps its filter and is evaluated again for the new
        // patient, whatever was in flight superseded
        | OrderContextMsg.PatientChanged(Some pat, request), OrderContextState.Shown ctx
        | OrderContextMsg.PatientChanged(Some pat, request), OrderContextState.Recalculating(ctx, _) ->
            evaluate { ctx with Patient = pat } request

        // a filter before a patient waits; with a patient it is evaluated at once
        | OrderContextMsg.Seed(ctx, _), OrderContextState.NoPatient
        | OrderContextMsg.Seed(ctx, _), OrderContextState.Seeded _ -> OrderContextState.Seeded ctx, []
        | OrderContextMsg.Seed(ctx, request), _ ->
            let pat = patient state |> Option.get
            evaluate { ctx with Patient = pat } request

        // a command over the workbench shown, always for the patient held: one at a time; an
        // update of the filter takes the formulary and the parenteralia along
        | OrderContextMsg.Command(cmd, ctx, request), OrderContextState.Shown shown ->
            let ctx = { ctx with Patient = shown.Patient }

            match cmd with
            | OrderContextCommand.UpdateOrderContext -> evaluate ctx request
            | _ -> OrderContextState.Recalculating(ctx, request), [ OrderContextEffect.CallContext(cmd, ctx, request) ]
        // a filter chosen before a patient is set waits as the seed
        | OrderContextMsg.Command(_, ctx, _), OrderContextState.NoPatient
        | OrderContextMsg.Command(_, ctx, _), OrderContextState.Seeded _ -> OrderContextState.Seeded ctx, []
        | OrderContextMsg.Command _, _ -> state, []

        // the answer lands only on the request it answers
        | OrderContextMsg.Answered(request, result), OrderContextState.Loading(pat, inFlight) when request = inFlight ->
            match result with
            | Ok ctx -> OrderContextState.Shown ctx, []
            | Error errs when noDoseRules errs ->
                // the filter matched nothing: the empty workbench evaluated again, the page left
                OrderContextState.Loading(pat, request),
                [
                    OrderContextEffect.GoToLifeSupport
                    OrderContextEffect.TellError errs
                    OrderContextEffect.CallContext(OrderContextCommand.UpdateOrderContext, emptyFor pat, request)
                ]
            | Error errs -> OrderContextState.Shown(emptyFor pat), [ OrderContextEffect.TellError errs ]
        | OrderContextMsg.Answered(request, result), OrderContextState.Recalculating(ctx, inFlight) when
            request = inFlight
            ->
            match result with
            | Ok answer -> OrderContextState.Shown answer, []
            | Error errs when noDoseRules errs ->
                OrderContextState.Loading(ctx.Patient, request),
                [
                    OrderContextEffect.GoToLifeSupport
                    OrderContextEffect.TellError errs
                    OrderContextEffect.CallContext(
                        OrderContextCommand.UpdateOrderContext,
                        emptyFor ctx.Patient,
                        request
                    )
                ]
            // a refused command leaves the workbench as the request found it
            | Error errs -> OrderContextState.Shown ctx, [ OrderContextEffect.TellError errs ]
        | OrderContextMsg.Answered _, _ -> state, []

        // the workbench cleared for the patient held and evaluated empty; nothing to clear
        // without a patient
        | OrderContextMsg.Reset request, OrderContextState.Shown ctx
        | OrderContextMsg.Reset request, OrderContextState.Recalculating(ctx, _) ->
            evaluate (emptyFor ctx.Patient) request
        | OrderContextMsg.Reset request, OrderContextState.Loading(pat, _) -> evaluate (emptyFor pat) request
        | OrderContextMsg.Reset _, _ -> state, []
