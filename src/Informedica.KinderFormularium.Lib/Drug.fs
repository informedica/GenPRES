namespace Informedica.KinderFormularium.Lib


module Drug =

    open Informedica.Utils.Lib.BCL


    module Frequency =

        type Frequency =
            | Frequency of Quantity
            | PRN of Quantity
            | AnteNoctum
            | Once
            | Bolus
        and Quantity =
            {
                Min : int
                Max : int
                Time : int
                Unit : string
            }

        let isValid = function
            | Frequency fr
            | PRN fr ->
                fr.Max > 0 && fr.Time > 0 && fr.Unit
                |> String.notEmpty
            | _ -> true


    module MinMax =

        type MinMax = { Min : float Option; Max : float Option }


    module Target =

        type Target =
            | Target of TargetType * TargetAge * TargetWeight
            | Unknown of string * string
        and TargetType =
            | AllType
            | Girl
            | Boy
            | Neonate
            | Aterm
            | Premature
        and TargetAge =
            | AllAge
            | Age of QuantityUnit Option * QuantityUnit Option
            | Pregnancy of QuantityUnit Option * QuantityUnit Option
            | PostConc of QuantityUnit Option * QuantityUnit Option
        and TargetWeight =
            | AllWeight
            | Weight of QuantityUnit Option * QuantityUnit Option
            | BirthWeight of QuantityUnit Option * QuantityUnit Option
        and QuantityUnit = { Quantity : float; Unit : string }


        let createQuantity v u = { Quantity = v; Unit = u }


        let getTarget targ =
            match targ with
            | Target (tt , ta , tw) -> (tt, ta, tw) |> Some
            | Unknown _ -> None


        let getQuantityUnit { Quantity = q; Unit = u} = (q, u)


    type Schedule =
        {
            TargetText : string
            Target : Target.Target
            FrequencyText : string
            Frequency : Frequency.Frequency Option
            ValueText : string
            Value : MinMax.MinMax Option
            Unit : string
        }


    type Route =
        {
            Name : string
            Schedules : Schedule list
        }


    type Dose =
        {
            Indication : string
            Routes : Route list
        }


    type Drug =
        {
            Id : string
            Atc: string
            Generic : string
            Brand : string
            Doses : Dose list
        }

    let createDrug id atc gen br =
        {
            Id = id
            Atc = atc
            Generic = gen
            Brand = br
            Doses = []
        }




