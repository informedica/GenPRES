namespace Informedica.GenPRES.Client.Core.StateMachines

/// Tracks the patient data the panel edits and sends each change to the server, which returns the completed
/// patient data. The App carries out the effects.
///
/// Two invariants:
/// - only one patient data update runs at a time: the panel accepts no edit meanwhile, and an answer is used
///   only when its request id matches;
/// - the workbench, the plan and the pages get the patient data from the server answer, never the draft.
module PatientMachine =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// A message to the patient machine. A message that starts a request carries the request id, so the answer
    /// can be matched to it.
    [<RequireQualifiedAccess>]
    type PatientMsg =
        /// The patient data from the panel, the url or the Session, with whether the estimates are renewed when
        /// the answer arrives.
        | Changed of PatientData option * PatientDraftPolicy.Estimates * request: string
        /// The patient data read from the EHR by a refresh. The EHR does not store the adult mark, so the adult
        /// mark of the draft is used; the server checks the mark against the age read.
        | Refreshed of PatientData option * request: string
        /// The age from the Session when a version opens; the draft keeps its data and takes this age.
        | AgeTaken of PatientData option * request: string
        /// The server answer or the errors for a request.
        | Answered of request: string * Result<PatientData, string[]>
        /// The plan could not use the answered patient data, so the same data can be sent again.
        | PlanNotFollowed
        /// Sends the draft again, for a plan that could not use the earlier answer.
        | GiveAgain of request: string


    /// An effect the App carries out for the patient machine.
    [<RequireQualifiedAccess>]
    type PatientEffect =
        /// Sends the patient data to the server with this request id.
        | CallPatient of PatientData * request: string
        /// Sets the patient data on the workbench, the plan and the formulary and parenteralia pages;
        /// None clears it.
        | SetPatientData of PatientData option
        /// Shows these errors to the user.
        | TellError of string[]
        /// Gives the plan the current patient data, so it can open a waiting version, without a data update.
        | GivePlan of PatientData


    /// The state of the patient machine.
    type PatientState =
        private
            {
                /// The patient data the panel edits and the lists read.
                Draft: PatientData option
                /// The patient data from the last server answer, as used by the workbench, the plan and the
                /// pages. None after the plan could not use it, so the same data can be sent again.
                Answered: PatientData option
                /// The update in progress: whether the estimates are renewed, the draft before the update
                /// (restored on a failure) and the request id the answer must match.
                InFlight: (PatientDraftPolicy.Estimates * PatientData option * string) option
            }


    module PatientState =

        /// Creates the state with the starting draft from the url, or None.
        let init (draft: PatientData option) =
            {
                Draft = draft
                Answered = None
                InFlight = None
            }


        /// Returns the patient data the panel edits.
        let draft (state: PatientState) = state.Draft


        /// Returns the patient data from the last server answer; None before an answer or after a clear.
        let answered (state: PatientState) = state.Answered


        /// Returns the request id of the update in progress; None when no update runs.
        let inFlightRequest (state: PatientState) = state.InFlight |> Option.map (fun (_, _, request) -> request)


        /// Whether an update is in progress. The panel accepts no edit until the answer, because an edit made
        /// meanwhile would miss the estimates the answer brings.
        let changing (state: PatientState) = state.InFlight.IsSome


        /// Returns the draft when it is valid patient data: with an age, or a measured weight and height.
        let patient (state: PatientState) =
            state.Draft |> Option.bind (PatientData.validate >> Result.toOption)


        /// Returns the patient data for the plan: the last answer, else the valid draft.
        let current (state: PatientState) = state.Answered |> Option.orElse (patient state)


        /// Returns the effect that gives the plan the current patient data; no effect when there is none.
        let givePlan (state: PatientState) =
            current state |> Option.map PatientEffect.GivePlan |> Option.toList


        /// Returns the next state and effects for a message.
        /// A draft without an age or a measured weight and height is not sent, and the patient data is cleared.
        /// When the estimates are renewed, the draft takes the answer; otherwise the draft stays, so a cleared
        /// weight stays cleared. A failure restores the draft from before the update.
        /// The same data as the last answer, given while no update runs, is not sent again and changes nothing.
        /// An answer that cannot select rules, such as a child without a measured or estimated weight, is not
        /// passed on: the pages keep their patient data and the page notice says what is missing.
        /// When renewed estimates come with a new age, the server makes the estimates, because an estimate
        /// depends on the age; a measured value stays.
        let rec transition (msg: PatientMsg) (state: PatientState) : PatientState * PatientEffect list =
            match msg with
            // After a signature the Session gives the signed patient data again. The draft stays, so a weight
            // cleared before the signature stays cleared.
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
                // Uses the adult mark of the draft; the server sets the mark again for the age read.
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

                    if pat |> PatientData.selectsRules then
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

                    // A version waiting for this answer opens with the current patient data.
                    state, PatientEffect.TellError errs :: givePlan state
                // An answer to an earlier update is ignored.
                | _ -> state, []
