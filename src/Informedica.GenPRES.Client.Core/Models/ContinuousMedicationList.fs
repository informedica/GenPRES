namespace Informedica.GenPRES.Client.Core.Models

/// The continuous medication list: every infusion protocol of the continuousmeds sheet that
/// fits the weight, with the dose one mL per hour gives.
module ContinuousMedicationList =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// A number as the list writes it: Dutch, without trailing zeros.
    let toStr = decimal >> Decimal.toStringNumberNLWithoutTrailingZeros


    /// The protocols whose weight range holds the weight, by category and medication, each with
    /// the dose that one mL per hour gives at the weight; a protocol without a total volume is left
    /// out.
    let calculate wght (contMeds: ContinuousMedication list) =

        let calcDose qty vol wght unit doseU =
            let wght = if doseU |> String.contains "kg" then wght else 1.

            let f =
                let t =
                    match doseU with
                    | _ when doseU |> String.contains "dag" -> 24.
                    | _ when doseU |> String.contains "min" -> 1. / 60.
                    | _ -> 1.

                let u =
                    match unit, doseU with
                    | _ when unit = "mg" && doseU |> String.contains "microg" -> 1000.
                    | _ when unit = "mg" && doseU |> String.contains "nanog" -> 1000. * 1000.
                    | _ -> 1.

                1. * t * u

            let d = f * qty / vol / wght |> Math.fixPrecision 2

            d, doseU


        let printAdv min max unit = $"%s{min |> toStr} - %s{max |> toStr} %s{unit}"

        contMeds
        |> List.filter (fun m -> m.MinWeight <= wght && (wght < m.MaxWeight || m.MaxWeight = 0.))
        |> List.sortBy (fun med -> med.Category, med.Medication)
        |> List.collect (fun med ->
            let vol = med.Total
            // TODO: really ugly hack to meet specific dose calc
            // need to create a config structure for this
            let qty =
                if med.Quantity = 0. && med.Hospital = "Radboud UMC" && med.Medication = "morfine" then
                    wght / 2. |> int |> float
                else
                    med.Quantity

            if vol = 0. then
                []
            else
                let d, u = calcDose qty vol wght med.Unit med.DoseUnit

                [
                    { Intervention.emptyIntervention with
                        Hospital = med.Hospital
                        Category = med.Category
                        Name = med.Medication
                        Quantity = Some qty
                        QuantityUnit = med.Unit
                        Total = Some vol
                        TotalUnit = "mL"
                        Solution = med.Solution
                        InterventionDose = Some 1.
                        InterventionDoseUnit = "mL/uur"
                        SubstanceMaxDose = Some med.AbsMax
                        SubstanceDoseAdjust = Some d
                        SubstanceDoseAdjustUnit = u
                        SubstanceMinDoseAdjust = Some med.MinDose
                        SubstanceMaxDoseAdjust = Some med.MaxDose
                        SubstanceDoseText = $"1 mL/uur = %s{d |> toStr} {u}"
                        Text = printAdv med.MinDose med.MaxDose med.DoseUnit
                    }
                ]
        )
