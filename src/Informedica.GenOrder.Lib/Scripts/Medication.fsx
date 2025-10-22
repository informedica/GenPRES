
#time

// load demo or product cache


System.Environment.SetEnvironmentVariable("GENPRES_DEBUG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I")

#load "load.fsx"

open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib


open Medication


let valueUnitOptToString =
    Option.map (ValueUnit.toStringDecimalDutchShortWithPrec 2)
    >> Option.defaultValue ""


let minMaxToString (minMax : MinMax) =
    if minMax = MinMax.empty then ""
    else
        minMax
        |> MinMax.toString
            "min "
            "min "
            "max "
            "max "


// have to add this to Informedica.GenForm.Lib
module DoseLimit =

    let minMaxToString perDose (minMax : MinMax) =
        if minMax = MinMax.empty then ""
        else
            minMax
            |> MinMax.toString
                "min "
                "min "
                "max "
                "max "
            |> fun s ->
                $"{s}{perDose}"

    let normDoseToString perDose vu =
        match vu with
        | None    -> ""
        | Some vu ->
            $"{vu |> Utils.ValueUnit.toString 3}{perDose}"


    let toString (dl: DoseLimit) =
        let perDose = "/dosis"
        let emptyS = ""
        [
            $"{dl.Rate |> minMaxToString emptyS}"
            $"{dl.RateAdjust |> minMaxToString emptyS}"

            $"{dl.NormPerTimeAdjust |> normDoseToString emptyS} " +
            $"{dl.PerTimeAdjust |> minMaxToString emptyS}"

            $"{dl.PerTime |> minMaxToString emptyS}"

            $"{dl.NormQuantityAdjust |> normDoseToString perDose} " +
            $"{dl.QuantityAdjust |> minMaxToString perDose}"

            $"{dl.Quantity |> minMaxToString perDose}"
        ]
        |> List.map String.trim
        |> List.filter (String.isNullOrWhiteSpace >> not)
        |> String.concat ", "


module SolutionLimit =

    let minMaxToString (minMax : MinMax) =
        if minMax = MinMax.empty then ""
        else
            minMax
            |> MinMax.toString
                "min "
                "min "
                "max "
                "max "

    let toString (sl: SolutionLimit) =
        [
            sl.Concentration |> minMaxToString
        ]
        |> String.concat "\n"


let limitOptToString =
    Option.map DoseLimit.toString
    >> Option.defaultValue ""

let solutionLimitOptToString =
    Option.map SolutionLimit.toString
    >> Option.defaultValue ""


module SubstanceItem =


    let create nme conc dos sol =
        {
            Name = nme
            Concentrations = conc
            Dose = dos
            Solution = sol
        }


    let toString (subst: SubstanceItem) =
        [
            "Name", subst.Name
            "Concentrations", subst.Concentrations |> valueUnitOptToString
            "Dose", subst.Dose |> limitOptToString
            "Solution", subst.Solution |> solutionLimitOptToString
        ]


SubstanceItem.create
    "paracetamol"
    ([| 60N; 120N; 240N; 500N |] |> ValueUnit.withUnit (Units.Mass.milliGram |> Units.per (Units.General.general "stuk")) |> Some)
    None
    None
|> SubstanceItem.toString


module ProductComponent =

    let create
        nme
        shp
        qts
        div
        dos
        sol
        sbs
        : ProductComponent
        =
        {
            Name = nme
            Shape = shp
            Quantities = qts
            Divisible = div
            Dose = dos
            Solution = sol
            Substances = sbs
        }

    let toString (prodCmp : ProductComponent) =
        [
            "Name", prodCmp.Name
            "Shape", prodCmp.Shape
            "Quantities", prodCmp.Quantities |> valueUnitOptToString
            "Divisible", prodCmp.Divisible |> BigRational.optToString
            "Dose", prodCmp.Dose |> limitOptToString
            "Solution", prodCmp.Solution |> solutionLimitOptToString
            "Substances", ""
        ]
        ,
        prodCmp.Substances |> List.map SubstanceItem.toString


let toString (med: Medication) =
    med.Components
    |> List.collect (fun prodCmp ->
        let sl, il = prodCmp |> ProductComponent.toString
        [
            sl |> List.map (fun (n, v) -> $"\t{n}: {v}")
            yield!
                il
                |> List.map (fun si ->
                    si
                    |> List.map (fun (n, v) -> $"\t\t{n}: {v}")
                    |> List.append [ "" ]
                )
        ]
    )
    |> List.append (
        [
            "Id", med.Id
            "Name", med.Name
            "Quantities", med.Quantities |> valueUnitOptToString
            "Route", med.Route
            "OrderType", $"{med.OrderType}"
            "Adjust", med.Adjust |> valueUnitOptToString
            "AdjustUnit", med.AdjustUnit |> Option.map Units.toStringEngShort |> Option.defaultValue ""
            "Frequencies", med.Frequencies |> valueUnitOptToString
            "Time", med.Time |> minMaxToString
            "Dose", med.Dose |> limitOptToString
            "DoseCount", med.DoseCount |> minMaxToString
            "Components", ""
        ]
        |> List.map (fun (n, v) -> [ $"{n}: {v}" ])
    )
    |> List.collect id

order
|> toString


let cotrim =
    {
        Medication.order with
            Id = "1"
            Name = "cotrimoxazol"
            Components =
                [
                    {
                        Medication.productComponent with
                            Name = "cotrimoxazol"
                            Shape = "tablet"
                            Quantities =
                                1N
                                |> ValueUnit.singleWithUnit Units.Count.times
                                |> Some
                            Divisible = Some (1N)
                            Substances =
                                [
                                    {
                                        Medication.substanceItem with
                                            Name = "sulfamethoxazol"
                                            Concentrations =
                                                [| 100N; 400N; 800N |]
                                                |> ValueUnit.withUnit Units.Mass.milliGram
                                                |> Some
                                    }
                                    {
                                        Medication.substanceItem with
                                            Name = "trimethoprim"
                                            Concentrations =
                                                [| 20N; 80N; 160N |]
                                                |> ValueUnit.withUnit Units.Mass.milliGram
                                                |> Some
                                    }
                                ]
                    }
                ]
            Route = "or"
            OrderType = DiscontinuousOrder
            Frequencies =
                [|2N |]
                |> ValueUnit.withUnit (Units.Count.times |> Units.per Units.Time.day)
                |> Some
    }


cotrim
|> toString
|> List.iter (printfn "%s")


cotrim
|> Medication.toOrderDto
|> Order.Dto.fromDto
|> Result.map Order.toString