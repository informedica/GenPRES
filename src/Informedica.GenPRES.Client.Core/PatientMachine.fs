/// Tracks the patient: the draft the panel edits and the patient change under way, which the
/// server answers with the patient made complete. The App carries out the effects.
///
/// Two invariants:
/// - one patient change is under way at a time; a newer change replaces it, and an answer lands
///   only on the request it names;
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
            /// What becomes of the estimates when the change under way is answered, and the
            /// request id its answer must name.
            InFlight: (PatientDraftPolicy.Estimates * string) option
        }


module PatientState =

    /// The draft as the App starts with it, from the url or none.
    let init (draft: Patient option) =
        {
            Draft = draft
            InFlight = None
        }


    /// The patient data as the panel edits it.
    let draft (state: PatientState) = state.Draft


    /// The request id the state waits on; None while no change is under way.
    let inFlightRequest (state: PatientState) = state.InFlight |> Option.map snd


    /// The patient the draft is: one with an age, or a measured weight and height.
    let patient (state: PatientState) = state.Draft |> Option.bind (Patient.validate >> Result.toOption)


    /// The next state and effects for a message. A draft below the minimum is no patient: nothing
    /// is sent and the patient is cleared. After an edit that renews the estimates the draft takes
    /// the answered patient; after any other edit it is kept, so a cleared weight stays cleared.
    let transition (msg: PatientMsg) (state: PatientState) : PatientState * PatientEffect list =
        match msg with
        | PatientMsg.Changed(dto, estimates, request) ->
            let state = { state with Draft = dto }

            match patient state with
            | Some pat ->
                { state with InFlight = Some(estimates, request) }, [ PatientEffect.CallPatient(pat, request) ]
            | None -> { state with InFlight = None }, [ PatientEffect.SetPatient None ]

        | PatientMsg.Answered(request, result) ->
            match state.InFlight, result with
            | Some(estimates, underWay), Ok pat when underWay = request ->
                let draft =
                    match estimates with
                    | PatientDraftPolicy.Estimates.Renewed -> Some pat
                    | PatientDraftPolicy.Estimates.Kept -> state.Draft

                {
                    Draft = draft
                    InFlight = None
                },
                [ PatientEffect.SetPatient(Some pat) ]
            | Some(_, underWay), Error errs when underWay = request ->
                { state with InFlight = None }, [ PatientEffect.TellError errs ]
            // an answer to an earlier change
            | _ -> state, []
