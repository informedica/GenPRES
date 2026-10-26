/// The emergency list as a table: the calculation of every entry of the emergencylist sheet over a
/// grid of weights and a grid of ages, one row per entry and grid point. The table is the
/// reference the emergency calculations are compared with when they move.
module Informedica.GenPRES.Shared.Tests.EmergencyBaseline

open System.Globalization
open Informedica.GenPRES.Shared


/// The weights in kg the table is calculated for.
let weights = [ 0.5; 1.; 2.; 3.; 5.; 7.5; 10.; 15.; 20.; 30.; 40.; 50.; 70.; 100. ]


/// The ages in years the table is calculated for, at the weight below. An age decides only the
/// tube and its lengths, so these rows keep only what the age adds.
let ages = [ 0.; 1.; 5.; 10.; 18. ]


/// The weight in kg the ages are calculated at; at 3 kg or more the age counts.
let ageWeight = 20.


/// A number as written in the table: invariant, round-trip exact.
let num (x: float) = x.ToString("R", CultureInfo.InvariantCulture)


/// An optional number as written in the table, empty when none.
let opt x = x |> Option.map num |> Option.defaultValue ""


/// The columns of the table.
let header =
    [
        "age"
        "weight"
        "hospital"
        "category"
        "name"
        "substanceDose"
        "substanceDoseUnit"
        "substanceDoseAdjust"
        "substanceDoseAdjustUnit"
        "volume"
        "volumeUnit"
        "concentration"
        "concentrationUnit"
        "substanceDoseText"
        "interventionDoseText"
        "text"
    ]


/// The rows of one age and weight.
let rowsOf bolus age weight =
    Models.EmergencyTreatment.calculate age (Some weight) bolus
    |> List.map (fun i ->
        [
            opt age
            num weight
            i.Hospital
            i.Category
            i.Name
            opt i.SubstanceDose
            i.SubstanceDoseUnit
            opt i.SubstanceDoseAdjust
            i.SubstanceDoseAdjustUnit
            opt i.InterventionDose
            i.InterventionDoseUnit
            opt i.Quantity
            i.QuantityUnit
            i.SubstanceDoseText
            i.InterventionDoseText
            i.Text
        ]
    )


/// The rows an age adds at the age weight: the rows of that age that the weight alone does not
/// give.
let ageRowsOf bolus age =
    let byWeight = rowsOf bolus None ageWeight |> List.map List.tail |> Set.ofList

    rowsOf bolus (Some age) ageWeight
    |> List.filter (fun row -> byWeight |> Set.contains (List.tail row) |> not)


/// The table for the rows of the emergencylist sheet, as tab separated text.
let tsv (sheet: string[][]) =
    let bolus = sheet |> Models.EmergencyTreatment.parse

    [
        header
        for weight in weights do
            yield! rowsOf bolus None weight
        for age in ages do
            yield! ageRowsOf bolus age
    ]
    |> List.map (String.concat "\t")
    |> String.concat "\n"
