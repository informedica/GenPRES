namespace Informedica.GenPRES.Client.Core.Models

/// What the client reads from a patient: the age in years and the weight in kg the lists are
/// calculated for, and the values the panel's fields show.
module PatientRead =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Models.Patient


    let getGAWeeks (p: Patient) = p.GestationalAge |> Option.map _.Weeks


    let getGADays (p: Patient) = p.GestationalAge |> Option.map _.Days


    let getRenalFunction (p: Patient) = p.RenalFunction |> Option.map RenalFunctionText.renalToOption


    let getAgeInYears p =
        [
            p |> getAgeYears |> Option.map float
            p |> getAgeMonths |> Option.map (fun ms -> (ms |> float) / 12.)
            p |> getAgeWeeks |> Option.map (fun ws -> (ws |> float) / 52.)
            p |> getAgeDays |> Option.map (fun ds -> (ds |> float) / 365.)
        ]
        |> List.choose id
        |> function
            | [] -> None
            | xs -> xs |> List.sum |> Some


    let getWeightInKg (pat: Patient) = pat |> getWeight |> Option.map (fun x -> float x / 1000.)
