namespace Informedica.GenPRES.Client.Core.StateMachines

/// Tracks the patient data the panel shows and sends each edit or whole patient data to the server, which returns
/// the completed patient data. The App carries out the effects.
///
/// Two invariants:
/// - only one patient data update runs at a time: the panel accepts no edit meanwhile, and an answer is used
///   only when its request id matches;
/// - the workbench, the order plan and the pages get the patient data from the server answer, never the draft.
module PatientMachine =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
    open Informedica.GenPRES.Shared.Models


    /// A message to the patient machine. A message that starts a request carries the request id, so the answer
    /// can be matched to it.
    [<RequireQualifiedAccess>]
    type PatientMsg =
        /// Whole patient data from the url, the Session or the reset; None clears it.
        | Changed of PatientData option * request: string
        /// An edit of the panel and the patient data it applies to; None applies it to empty patient data.
        | Edited of PatientDataEdit * PatientData option * request: string
        /// The patient data read from the EHR by a refresh. The EHR does not store the adult mark, so the adult
        /// mark of the draft is used; the server checks the mark against the age read.
        | Refreshed of PatientData option * request: string
        /// The age from the Session when a version opens; the draft keeps its data and takes this age.
        | AgeTaken of PatientData option * request: string
        /// The server answer or the errors for a request.
        | Answered of request: string * Result<PatientData, string[]>
        /// The order plan could not use the answered patient data, so the same data can be sent again.
        | PlanNotFollowed
        /// Sends the draft again, for an order plan that could not use the earlier answer.
        | GiveAgain of request: string


    /// An effect the App carries out for the patient machine.
    [<RequireQualifiedAccess>]
    type PatientEffect =
        /// Sends the whole patient data to the server with this request id.
        | CallPatient of PatientData * request: string
        /// Sends an edit and the patient data it applies to the server with this request id.
        | CallEdit of PatientDataEdit * PatientData * request: string
        /// Sets the patient data on the workbench, the order plan and the formulary and parenteralia pages;
        /// None clears it.
        | SetPatientData of PatientData option
        /// Shows these errors to the user.
        | TellError of string[]
        /// Gives the order plan the current patient data, so it can open a waiting version, without a data
        /// update.
        | GivePlan of PatientData


    /// The state of the patient machine.
    type PatientState =
        private
            {
                /// The patient data the panel shows: the last answer, or the whole patient data sent.
                Draft: PatientData option
                /// The patient data from the last server answer, as used by the workbench, the order plan and the
                /// pages. None after the order plan could not use it, so the same data can be sent again.
                Answered: PatientData option
                /// The update in progress: the draft before the update (restored on a failure) and the request id
                /// the answer must match.
                InFlight: (PatientData option * string) option
            }


    module PatientState =

        /// Creates the state with the starting draft from the url, or None.
        let init (draft: PatientData option) =
            {
                Draft = draft
                Answered = None
                InFlight = None
            }


        /// Returns the patient data the panel shows.
        let draft (state: PatientState) = state.Draft


        /// Returns the patient data from the last server answer; None before an answer or after a clear.
        let answered (state: PatientState) = state.Answered


        /// Returns the request id of the update in progress; None when no update runs.
        let inFlightRequest (state: PatientState) = state.InFlight |> Option.map snd


        /// Whether an update is in progress. The panel accepts no edit until the answer, because the next edit
        /// applies to the answer.
        let changing (state: PatientState) = state.InFlight.IsSome


        /// Returns the draft when it is valid patient data: with an age, or a measured weight and height.
        let patient (state: PatientState) =
            state.Draft |> Option.bind (PatientData.validate >> Result.toOption)


        /// Returns the patient data for the order plan: the last answer, else the valid draft.
        let current (state: PatientState) = state.Answered |> Option.orElse (patient state)


        /// Returns the effect that gives the order plan the current patient data; no effect when there is none.
        let givePlan (state: PatientState) =
            current state |> Option.map PatientEffect.GivePlan |> Option.toList


        /// Returns the next state and effects for a message.
        /// Whole patient data without an age or a measured weight and height is not sent, and the patient data is
        /// cleared. An edit is always sent; the server applies it and completes the result. Every answer becomes
        /// the draft. A failure restores the draft from before the update.
        /// The same data as the last answer, given while no update runs, is not sent again and changes nothing.
        /// An answer without an age or a measured weight and height clears the patient data. An answer that cannot
        /// select rules, such as a child without a measured or estimated weight, is not passed on: the pages keep
        /// their patient data and the page notice says what is missing.
        let rec transition (msg: PatientMsg) (state: PatientState) : PatientState * PatientEffect list =
            match msg with
            // After a signature the Session gives the signed patient data again; it is not sent again.
            | PatientMsg.Changed(Some pat, _) when state.InFlight.IsNone && state.Answered = Some pat -> state, []
            | PatientMsg.Changed(dto, request) ->
                let before = state.Draft
                let state = { state with Draft = dto }

                match patient state with
                | Some pat ->
                    { state with InFlight = Some(before, request) }, [ PatientEffect.CallPatient(pat, request) ]
                | None ->
                    { state with
                        Answered = None
                        InFlight = None
                    },
                    [ PatientEffect.SetPatientData None ]

            | PatientMsg.Edited(edit, pat, request) ->
                { state with InFlight = Some(state.Draft, request) },
                [
                    PatientEffect.CallEdit(edit, pat |> Option.defaultValue PatientData.empty, request)
                ]

            | PatientMsg.Refreshed(pat, request) ->
                // Uses the adult mark of the draft; the server sets the mark again for the age read.
                let read =
                    pat
                    |> Option.map (fun p ->
                        { p with AdultMark = state.Draft |> Option.map _.AdultMark |> Option.defaultValue p.AdultMark }
                    )

                transition (PatientMsg.Changed(read, request)) state

            | PatientMsg.AgeTaken(taken, request) ->
                match taken, state.Draft with
                | Some t, Some d when t.Age <> d.Age ->
                    transition (PatientMsg.Changed(Some { d with Age = t.Age }, request)) state
                | _ -> state, givePlan state

            | PatientMsg.PlanNotFollowed -> { state with Answered = None }, []

            | PatientMsg.GiveAgain request -> transition (PatientMsg.Changed(state.Draft, request)) state

            | PatientMsg.Answered(request, result) ->
                match state.InFlight, result with
                | Some(_, underWay), Ok pat when underWay = request ->
                    let state =
                        { state with
                            Draft = Some pat
                            InFlight = None
                        }

                    match pat |> PatientData.validate, pat |> PatientData.selectsRules with
                    | Ok _, true -> { state with Answered = Some pat }, [ PatientEffect.SetPatientData(Some pat) ]
                    | Ok _, false -> state, givePlan state
                    | Error _, _ -> { state with Answered = None }, [ PatientEffect.SetPatientData None ]
                | Some(before, underWay), Error errs when underWay = request ->
                    let state =
                        { state with
                            Draft = before
                            InFlight = None
                        }

                    // A version waiting for this answer opens with the current patient data.
                    state, PatientEffect.TellError errs :: givePlan state
                // An answer to an earlier update is ignored.
                | _ -> state, []
