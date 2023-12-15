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
        VenousAccess = if sc.CVL then [VenousAccess.CVL] else []
    }
    |> Patient.calcPMAge


