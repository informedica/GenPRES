namespace ServerApi

open System
open Shared.Types

module CoreConversions = Informedica.GenCore.Lib.Conversions
module CoreWeightValue = Informedica.GenCore.Lib.Patients.WeightValue
module CoreWeightAtDate = Informedica.GenCore.Lib.Patients.WeightAtDate
module CoreHeightValue = Informedica.GenCore.Lib.Patients.HeightValue
module CoreHeightAtDate = Informedica.GenCore.Lib.Patients.HeightAtDate
module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays


/// What the Session holds of the user's measurements. A weight, a height or a gestational age
/// the user measures at the bedside stays for the Session, whatever the EHR reads later, and a
/// resume restores it. The measurements are held beside the EHR data as read, never written
/// into it, so that the challenge keeps comparing the read as read; they go onto the contract
/// patient where the client resumes, and onto the core patient where the Session's patient is
/// projected, which the merge at a notice and a commit will do. The type of the same name
/// lives with the ports, so the module says so.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Measurements =

    let none: Measurements =
        {
            Weight = None
            Height = None
            GestAge = None
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


    /// The measurements a Session's rows come to, oldest first: the latest row per kind.
    let ofRows (rows: (Measurement * DateTime) list) : Measurements = rows |> List.fold withRow none


    /// What a request records: for each of the weight, the height and the gestational age, the
    /// draft's value when it differs from the one the Session stands on, the row held else the
    /// EHR's as shown; nothing for a draft that is no patient. The measurements after, and the
    /// rows to write.
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
            let ehr = shown |> Option.defaultValue Shared.Models.Patient.empty

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

            {
                Weight = weight
                Height = height
                GestAge = gestAge
            },
            [
                w |> Option.map Measurement.Weight
                h |> Option.map Measurement.Height
                g |> Option.map Measurement.GestAge
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


    /// The contract patient with the measurements on it, for the client that resumes: a value
    /// as the measured one, a cleared one as none, one never touched as shown.
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
        }
