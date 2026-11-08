namespace Informedica.GenPRES.Client.Core.StateMachines

/// Tracks the patient: the draft the panel edits and the patient change under way, which the
/// server answers with the patient made complete. The App carries out the effects.
///
/// Two invariants:
/// - one patient change is under way at a time: the panel takes no edit while one is,
///   and an answer lands only on the request it names;
/// - the workbench, the plan and the pages get the patient as the server answered it, never the
///   draft.
module PatientMachine =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// What moves the patient machine. A message that starts a request carries its request id, so
    /// the answer can name it.
    [<RequireQualifiedAccess>]
    type PatientMsg =
        /// The patient data from the panel, the url or the Session, with what becomes of the
        /// estimates once the change is answered.
        | Changed of Patient option * PatientDraftPolicy.Estimates * request: string
        /// The patient data read from the EHR by a refresh. The EHR does not hold the mark as an adult,
        /// so the mark of the draft stays, unless the age read is below the age a mark is allowed at.
        | Refreshed of Patient option * request: string
        /// The age the Session took at a version opened: the panel keeps its data and takes the age.
        | AgeTaken of Patient option * request: string
        /// The server's answer, or the failure, for a request.
        | Answered of request: string * Result<Patient, string[]>
        /// The plan could not follow the patient answered, so the same data can be given again.
        | PlanNotFollowed
        /// The data of the draft asked again, for a plan that did not follow them.
        | GiveAgain of request: string


    /// What the App carries out for the patient machine.
    [<RequireQualifiedAccess>]
    type PatientEffect =
        /// Send the patient change under this request id.
        | CallPatient of Patient * request: string
        /// Put the patient data on the workbench, the plan and the formulary and parenteralia pages;
        /// none clears them.
        | SetPatientData of Patient option
        /// Tell the user what went wrong.
        | TellError of string[]
        /// Give the plan the patient it can open a waiting version for, without any change of data.
        | GivePlan of Patient


    /// Everything the patient machine holds.
    type PatientState =
        private
            {
                /// The patient data as the panel edits it and the lists read it.
                Draft: Patient option
                /// The patient as the server last answered it, the one the workbench, the plan and the
                /// pages have; None once the plan could not follow it, so the same data given again goes
                /// out.
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


        /// The patient the plan can go on with: the one last answered, else the one the draft is.
        let current (state: PatientState) = state.Answered |> Option.orElse (patient state)


        /// What gives the plan the patient it can go on with; nothing when there is none.
        let givePlan (state: PatientState) =
            current state |> Option.map PatientEffect.GivePlan |> Option.toList


        /// The next state and effects for a message. A draft below the minimum is no patient: nothing
        /// is sent and the patient is cleared. After an edit that renews the estimates the draft takes
        /// the answered patient; after any other edit it is kept, so a cleared weight stays cleared. A
        /// failure puts back the draft the change started from, the one the orders were calculated for.
        /// The patient data the orders were calculated for, given again while no change is under way, is
        /// not sent again: the state, the draft included, stays as it is.
        /// An answered patient the rules are not looked up for, a child without a weight, measured or
        /// estimated, is held back: every request would be refused for it, so the pages keep the
        /// patient they have, and the notice on the page says what is missing.
        /// Renewed estimates with a new age leave the estimating to the server, since an estimate
        /// belongs to its age; a measured value stays.
        let rec transition (msg: PatientMsg) (state: PatientState) : PatientState * PatientEffect list =
            match msg with
            // the Session gives the patient as signed after a signature; the draft is kept, so a weight
            // cleared before the signature stays cleared
            | PatientMsg.Changed(Some pat, _, _) when state.InFlight.IsNone && state.Answered = Some pat -> state, []
            | PatientMsg.Changed(dto, estimates, request) ->
                let before = state.Draft

                let dto =
                    match estimates, before with
                    | PatientDraftPolicy.Estimates.Renewed, Some b -> dto |> Option.map (PatientEdit.renewEstimates b)
                    | _ -> dto

                let state = { state with Draft = dto }

                match patient state with
                | Some pat ->
                    { state with InFlight = Some(estimates, before, request) },
                    [ PatientEffect.CallPatient(pat, request) ]
                | None ->
                    { state with
                        Answered = None
                        InFlight = None
                    },
                    [ PatientEffect.SetPatientData None ]

            | PatientMsg.Refreshed(pat, request) ->
                // the mark of the draft goes with the reading; the server sets it again for the age
                let read =
                    pat
                    |> Option.map (fun p ->
                        { p with AdultMark = state.Draft |> Option.map _.AdultMark |> Option.defaultValue p.AdultMark }
                    )

                transition (PatientMsg.Changed(read, PatientDraftPolicy.Estimates.Renewed, request)) state

            | PatientMsg.AgeTaken(taken, request) ->
                match taken, state.Draft with
                | Some t, Some d when t.Age <> d.Age ->
                    transition
                        (PatientMsg.Changed(Some { d with Age = t.Age }, PatientDraftPolicy.Estimates.Renewed, request))
                        state
                | _ -> state, givePlan state

            | PatientMsg.PlanNotFollowed -> { state with Answered = None }, []

            | PatientMsg.GiveAgain request ->
                transition (PatientMsg.Changed(state.Draft, PatientDraftPolicy.Estimates.Kept, request)) state

            | PatientMsg.Answered(request, result) ->
                match state.InFlight, result with
                | Some(estimates, _, underWay), Ok pat when underWay = request ->
                    let draft =
                        match estimates with
                        | PatientDraftPolicy.Estimates.Renewed -> Some pat
                        | PatientDraftPolicy.Estimates.Kept -> state.Draft

                    if pat |> Patient.selectsRules then
                        {
                            Draft = draft
                            Answered = Some pat
                            InFlight = None
                        },
                        [ PatientEffect.SetPatientData(Some pat) ]
                    else
                        { state with
                            Draft = draft
                            InFlight = None
                        },
                        givePlan state
                | Some(_, before, underWay), Error errs when underWay = request ->
                    let state =
                        { state with
                            Draft = before
                            InFlight = None
                        }

                    // a version waiting for this answer opens on the data there are
                    state, PatientEffect.TellError errs :: givePlan state
                // an answer to an earlier change
                | _ -> state, []
