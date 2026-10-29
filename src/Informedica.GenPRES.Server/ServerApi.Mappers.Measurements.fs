namespace ServerApi

open System
open Informedica.GenPRES.Shared.Types

module CoreConversions = Informedica.GenCore.Lib.Conversions
module CoreWeightValue = Informedica.GenCore.Lib.Patients.WeightValue
module CoreWeightAtDate = Informedica.GenCore.Lib.Patients.WeightAtDate
module CoreHeightValue = Informedica.GenCore.Lib.Patients.HeightValue
module CoreHeightAtDate = Informedica.GenCore.Lib.Patients.HeightAtDate
module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays


/// The measurements the user enters during a Session: a weight, a height or a gestational age
/// measured at the bedside, and the mark as an adult. The Session keeps them for as long as it
/// lasts, even when the EHR later reads other values, and a resume puts them back. The EHR never
/// sets the mark as an adult; only the user sets or clears it.
///
/// The measurements are kept apart from the EHR data and never written into it, so that the
/// challenge can still compare the EHR data exactly as it was read. They are added to the patient
/// in three places: to the contract patient when the client resumes, to the core patient before
/// the Session's patient is projected from it, and, for the mark as an adult, to the GenFORM
/// patient after that projection, since the core patient has no mark.
///
/// The type with the same name is defined with the ports, so this module has the Module suffix.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Measurements =

    let none: Measurements =
        {
            Weight = None
            Height = None
            GestAge = None
            MarkedAdult = None
        }


    /// The measurements after one row: the row decides its kind.
    let withRow (held: Measurements) (row: Measurement, at: DateTime) : Measurements =
        match row with
        | Measurement.Weight w ->
            { held with
                Weight =
                    Some
                        {
                            Value = w
                            At = at
                        }
            }
        | Measurement.Height h ->
            { held with
                Height =
                    Some
                        {
                            Value = h
                            At = at
                        }
            }
        | Measurement.GestAge g ->
            { held with
                GestAge =
                    Some
                        {
                            Value = g
                            At = at
                        }
            }
        | Measurement.MarkedAdult marked ->
            { held with
                MarkedAdult =
                    Some
                        {
                            Value = Some marked
                            At = at
                        }
            }


    /// The measurements a Session's rows come to, oldest first: the latest row per kind.
    let ofRows (rows: (Measurement * DateTime) list) : Measurements = rows |> List.fold withRow none


    /// What a request records: for each of the weight, the height, the gestational age and the
    /// mark as an adult, the draft's value when it differs from the one the Session stands on, the
    /// row held else the EHR's as shown; nothing for a draft that is no patient. The measurements
    /// after, and the rows to write.
    let record
        (now: DateTime)
        (shown: Patient option)
        (held: Measurements)
        (draft: Patient)
        : Measurements * Measurement list
        =
        match draft |> Patient.patient with
        | Error _ -> held, []
        | Ok _ ->
            let ehr = shown |> Option.defaultValue Informedica.GenPRES.Shared.Models.Patient.empty

            // the value stood on, and the draft's when it is another
            let change (held: Measured<'v> option) (ehr: 'v option) (draft: 'v option) =
                let standing = held |> Option.map _.Value |> Option.defaultValue ehr

                if draft = standing then
                    held, None
                else
                    Some
                        {
                            Value = draft
                            At = now
                        },
                    Some draft

            let weight, w = change held.Weight ehr.Weight.Measured draft.Weight.Measured
            let height, h = change held.Height ehr.Height.Measured draft.Height.Measured
            let gestAge, g = change held.GestAge ehr.GestationalAge draft.GestationalAge
            let markedAdult, m = change held.MarkedAdult (Some ehr.MarkedAdult) (Some draft.MarkedAdult)

            {
                Weight = weight
                Height = height
                GestAge = gestAge
                MarkedAdult = markedAdult
            },
            [
                w |> Option.map Measurement.Weight
                h |> Option.map Measurement.Height
                g |> Option.map Measurement.GestAge
                m |> Option.flatten |> Option.map Measurement.MarkedAdult
            ]
            |> List.choose id


    /// The core patient with the measurements on it: a value as a dated actual and the
    /// calculation value, so that the projection reads it as measured; a cleared one leaves
    /// the actuals as read and no calculation value, so that the projection has none and the
    /// estimate stands; one never touched leaves the patient as read.
    let onCore
        (held: Measurements)
        (core: Informedica.GenCore.Lib.Patients.Patient)
        : Informedica.GenCore.Lib.Patients.Patient
        =
        let weight =
            match held.Weight with
            | None -> core.Weight
            | Some { Value = None } -> { core.Weight with Calculation = None }
            | Some {
                       Value = Some g
                       At = at
                   } ->
                let w = CoreWeightAtDate.create at (CoreWeightValue.weightInGram (decimal g))

                { core.Weight with
                    Actual = core.Weight.Actual @ [ w ]
                    Calculation = Some w
                }

        let height =
            match held.Height with
            | None -> core.Height
            | Some { Value = None } -> { core.Height with Calculation = None }
            | Some {
                       Value = Some cm
                       At = at
                   } ->
                let h = CoreHeightAtDate.create at (CoreHeightValue.heightInCm (decimal cm))

                { core.Height with
                    Actual = core.Height.Actual @ [ h ]
                    Calculation = Some h
                }

        let gestAge =
            match held.GestAge with
            | None -> core.GestationalAge
            | Some { Value = None } -> None
            | Some { Value = Some ga } ->
                CoreAgeWeeksDays.create
                    (CoreConversions.weekFromInt (int ga.Weeks))
                    (CoreConversions.dayFromInt (int ga.Days))
                |> Some

        { core with
            Weight = weight
            Height = height
            GestationalAge = gestAge
        }


    /// The mark as an adult the user set or cleared, else the mark given.
    let markedAdult (held: Measurements) given =
        held.MarkedAdult |> Option.bind _.Value |> Option.defaultValue given


    /// The GenFORM patient with the mark the user set or cleared on it. The core patient the EHR
    /// is projected from has no mark, so the mark goes on after the projection.
    let onLibPatient (held: Measurements) (pat: Informedica.GenForm.Lib.Types.Patient) =
        { pat with MarkedAdult = pat.MarkedAdult |> markedAdult held }


    /// The contract patient with the measurements on it, for the client that resumes: a value
    /// as the measured one, a cleared one as none, one never touched as shown; the mark as an
    /// adult the user set or cleared, else as shown.
    let apply (held: Measurements) (shown: Patient) : Patient =
        { shown with
            Weight =
                match held.Weight with
                | None -> shown.Weight
                | Some w -> { shown.Weight with Measured = w.Value }
            Height =
                match held.Height with
                | None -> shown.Height
                | Some h -> { shown.Height with Measured = h.Value }
            GestationalAge =
                match held.GestAge with
                | None -> shown.GestationalAge
                | Some g -> g.Value
            MarkedAdult = shown.MarkedAdult |> markedAdult held
        }
