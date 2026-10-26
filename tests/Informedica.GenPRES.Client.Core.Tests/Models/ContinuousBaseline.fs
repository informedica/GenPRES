/// The continuous medication list as a table: the calculation of every entry of the
/// continuousmeds sheet over a grid of weights, one row per entry and weight. The table is the
/// reference the continuous calculation is held to.
module Informedica.GenPRES.Client.Core.Tests.Models.ContinuousBaseline

open System.Globalization
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Client.Core.Models


/// The weights in kg the table is calculated for.
let weights = [ 0.5; 1.; 2.; 3.; 5.; 7.5; 10.; 15.; 20.; 30.; 40.; 50.; 70.; 100. ]


/// A number as written in the table: invariant, round-trip exact.
let num (x: float) = x.ToString("R", CultureInfo.InvariantCulture)


/// An optional number as written in the table, empty when none.
let opt x = x |> Option.map num |> Option.defaultValue ""


/// The columns of the table.
let header =
    [
        "weight"
        "hospital"
        "category"
        "name"
        "quantity"
        "quantityUnit"
        "total"
        "totalUnit"
        "solution"
        "doseAtOneMlPerHour"
        "doseUnit"
        "minDose"
        "maxDose"
        "absMax"
        "substanceDoseText"
        "text"
    ]


/// The rows of one weight.
let rowsOf meds weight =
    ContinuousMedicationList.calculate weight meds
    |> List.map (fun (i: Intervention) ->
        [
            num weight
            i.Hospital
            i.Category
            i.Name
            opt i.Quantity
            i.QuantityUnit
            opt i.Total
            i.TotalUnit
            i.Solution
            opt i.SubstanceDoseAdjust
            i.SubstanceDoseAdjustUnit
            opt i.SubstanceMinDoseAdjust
            opt i.SubstanceMaxDoseAdjust
            opt i.SubstanceMaxDose
            i.SubstanceDoseText
            i.Text
        ]
    )


/// The table for the rows of the continuousmeds sheet, as tab separated text.
let tsv (sheet: string[][]) =
    let meds = sheet |> Models.ContinuousMedication.parse

    [
        header
        for weight in weights do
            yield! rowsOf meds weight
    ]
    |> List.map (String.concat "\t")
    |> String.concat "\n"
