module Mapping


open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


open Shared.Types


let mapPatient
    (sc: ScenarioResult)
    =
    { Patient.patient with
        Department =
            sc.Department
            |> Option.defaultValue "ICK"
            |> Some
        Age =
            sc.AgeInDays
            |> Option.bind BigRational.fromFloat
            |> Option.map (ValueUnit.singleWithUnit Units.Time.day)
        GestAge =
            sc.GestAgeInDays
            |> Option.map BigRational.fromInt
            |> Option.map (ValueUnit.singleWithUnit Units.Time.day)
        Weight =
            sc.WeightInKg
            |> Option.bind BigRational.fromFloat
            |> Option.map (ValueUnit.singleWithUnit Units.Weight.kiloGram)
        Height =
            sc.HeightInCm
            |> Option.bind BigRational.fromFloat
            |> Option.map (ValueUnit.singleWithUnit Units.Height.centiMeter)
        Gender =
            match sc.Gender with
            | Male -> Informedica.GenForm.Lib.Types.Male
            | Female -> Informedica.GenForm.Lib.Types.Female
            | UnknownGender -> AnyGender
        Locations =
            sc.AccessList
            // TODO make proper mapping
            |> List.choose (fun a ->
                match a with
                | CVL -> Informedica.GenForm.Lib.Types.CVL |> Some
                | PVL -> Informedica.GenForm.Lib.Types.PVL |> Some
                | _ -> None
            )
        RenalFunction =
            sc.RenalFunction
            |> Option.map (fun rf ->
                match rf with
                | EGFR(min, max) -> Informedica.GenForm.Lib.Types.RenalFunction.EGFR(min, max)
                | IntermittendHemoDialysis -> Informedica.GenForm.Lib.Types.RenalFunction.IntermittentHemodialysis
                | ContinuousHemoDialysis -> Informedica.GenForm.Lib.Types.RenalFunction.ContinuousHemodialysis
                | PeritionealDialysis -> Informedica.GenForm.Lib.Types.RenalFunction.PeritonealDialysis
            )
    }
    |> fun p ->
        printfn $"patient mapped: {p}"
        p
    |> Patient.calcPMAge


