
#time

// load demo or product cache


System.Environment.SetEnvironmentVariable("GENPRES_DEBUG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I")

#load "load.fsx"

open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib


open Medication


let valueUnitOptToString =
    Option.map (ValueUnit.toStringDecimalDutchShortWithPrec 2)
    >> Option.defaultValue ""

// have to add this to Informedica.GenForm.Lib
module DoseLimit =

    open Informedica.

    let printMinMaxDose perDose (minMax : MinMax) =
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

    let printNormDose perDose vu =
        match vu with
        | None    -> ""
        | Some vu ->
            $"{vu |> Utils.ValueUnit.toString 3}{perDose}"


    let toString (dl: DoseLimit) =
        [
            let perDose = "/dosis"
            let emptyS = ""
            [
                $"{dl.Rate |> printMinMaxDose emptyS}"
                $"{dl.RateAdjust |> printMinMaxDose emptyS}"

                $"{dl.NormPerTimeAdjust |> printNormDose emptyS} " +
                $"{dl.PerTimeAdjust |> printMinMaxDose emptyS}"

                $"{dl.PerTime |> printMinMaxDose emptyS}"

                $"{dl.NormQuantityAdjust |> printNormDose perDose} " +
                $"{dl.QuantityAdjust |> printMinMaxDose perDose}"

                $"{dl.Quantity |> printMinMaxDose perDose}"
            ]
            |> List.map String.trim
            |> List.filter (String.IsNullOrEmpty >> not)
            |> String.concat ", "
        ]



let limitOptToString =
    Option.map (DoseLimit.toString false)
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
            "Dose", subst.Dose
        ]


let toString (med: Medication) =
    [
        "Id", med.Id
        "Name", med.Name
        "Adjust", med.Adjust |> valueUnitOptToString
    ]
    |> List.map (fun (n, v) -> $"{n}: {v}")

order
|> toString