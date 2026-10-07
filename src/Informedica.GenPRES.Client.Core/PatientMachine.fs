/// Tracks the patient: the draft the panel edits and the patient change under way, which the
/// server answers with the patient made complete. The App carries out the effects.
///
/// Two invariants:
/// - one patient change is under way at a time: the panel takes no edit while one is,
///   and an answer lands only on the request it names;
/// - the workbench, the plan and the pages get the patient as the server answered it, never the
///   draft.
module PatientMachine

open Shared.Types
open Shared.Models


/// What moves the patient machine. A message that starts a request carries its request id, so
/// the answer can name it.
[<RequireQualifiedAccess>]
type PatientMsg =
    /// The patient data from the panel, the url or the Session, with what becomes of the
    /// estimates once the change is answered.
    | Changed of Patient option * PatientDraftPolicy.Estimates * request: string
    /// The server's answer, or the failure, for a request.
    | Answered of request: string * Result<Patient, string[]>


/// What the App carries out for the patient machine.
[<RequireQualifiedAccess>]
type PatientEffect =
    /// Send the patient change under this request id.
    | CallPatient of Patient * request: string
    /// Put the patient on the workbench, the plan and the formulary and parenteralia pages; none
    /// clears them.
    | SetPatient of Patient option
    /// Tell the user what went wrong.
    | TellError of string[]


/// Everything the patient machine holds.
type PatientState =
    private
        {
            /// The patient data as the panel edits it and the lists read it.
            Draft: Patient option
            /// The patient as the server last answered it, the one the workbench, the plan and the
            /// pages have.
            Answered: Patient option
            /// What becomes of the estimates when the change under way is answered, the draft it
            /// started from, which a failure puts back, and the request id its answer must name.
            InFlight: (PatientDraftPolicy.Estimates * Patient option * string) option
        }


module PatientState =

    /// The draft as the App starts with it, from the url or none.
    let init (draft: Patient option) =
        {
            Draft = draft
            Answered = None
            InFlight = None
        }


    /// The patient data as the panel edits it.
    let draft (state: PatientState) = state.Draft


    /// The patient as the server last answered it; None before an answer or once cleared.
    let answered (state: PatientState) = state.Answered


    /// The request id the state waits on; None while no change is under way.
    let inFlightRequest (state: PatientState) = state.InFlight |> Option.map (fun (_, _, request) -> request)


    /// Whether a patient change is under way, so that the panel takes no edit until it is answered:
    /// an edit made meanwhile would start from a draft without the estimates the answer brings.
    let changing (state: PatientState) = state.InFlight.IsSome


    /// The patient the draft is: one with an age, or a measured weight and height.
    let patient (state: PatientState) = state.Draft |> Option.bind (Patient.validate >> Result.toOption)


    /// The next state and effects for a message. A draft below the minimum is no patient: nothing
    /// is sent and the patient is cleared. After an edit that renews the estimates the draft takes
    /// the answered patient; after any other edit it is kept, so a cleared weight stays cleared. A
    /// failure puts back the draft the change started from, the one the orders were calculated for.
    /// An answered patient without a weight or a height, measured or estimated, is held back: every
    /// request would be refused for it, so the pages keep the patient they have, and the notice on
    /// the page says what is missing.
    let transition (msg: PatientMsg) (state: PatientState) : PatientState * PatientEffect list =
        match msg with
        | PatientMsg.Changed(dto, estimates, request) ->
            let before = state.Draft
            let state = { state with Draft = dto }

            match patient state with
            | Some pat ->
                { state with InFlight = Some(estimates, before, request) }, [ PatientEffect.CallPatient(pat, request) ]
            | None ->
                { state with
                    Answered = None
                    InFlight = None
                },
                [ PatientEffect.SetPatient None ]

        | PatientMsg.Answered(request, result) ->
            match state.InFlight, result with
            | Some(estimates, _, underWay), Ok pat when underWay = request ->
                let draft =
                    match estimates with
                    | PatientDraftPolicy.Estimates.Renewed -> Some pat
                    | PatientDraftPolicy.Estimates.Kept -> state.Draft

                match pat |> Patient.getWeight, pat |> Patient.getHeight with
                | Some _, Some _ ->
                    {
                        Draft = draft
                        Answered = Some pat
                        InFlight = None
                    },
                    [ PatientEffect.SetPatient(Some pat) ]
                | _ ->
                    { state with
                        Draft = draft
                        InFlight = None
                    },
                    []
            | Some(_, before, underWay), Error errs when underWay = request ->
                { state with
                    Draft = before
                    InFlight = None
                },
                [ PatientEffect.TellError errs ]
            // an answer to an earlier change
            | _ -> state, []
