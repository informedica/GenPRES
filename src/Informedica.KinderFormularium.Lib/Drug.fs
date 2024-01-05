namespace Informedica.KinderFormularium.Lib


module Drug =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

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
                fr.Max > 0 && fr.Time > 0 &&
                fr.Unit
                |> String.notEmpty
            | _ -> true


        let toDoseType = function
            | Frequency _ -> "onderhoud"
            | PRN _ -> "prn"
            | AnteNoctum -> "onderhoud"
            | Once -> "eenmalig"
            | Bolus -> "eenmalig"


        let getFrequency freq =
            match freq with
            | PRN qty
            | Frequency qty ->
                [qty.Min .. 1.. qty.Max ]
                |> List.map string
                |> String.concat ";"
            | _ -> ""



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
            | GestationalAge of QuantityUnit Option * QuantityUnit Option
            | PostMenstrualAge of QuantityUnit Option * QuantityUnit Option
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


        let getGender target =
            match target |> getTarget with
            | Some (tt, _, _) -> tt |> Some
            | _ -> None


        let getAge target =
            match target |> getTarget with
            | Some (_, age, _) ->
                match age with
                | Age (min, max) -> min, max
                | GestationalAge _
                | PostMenstrualAge _ -> None, Some { Quantity = 6.; Unit = "months" }
                | _ -> None, None
            | _ -> None, None


        let getAgeInDays target =
            let toDays v u =
                u
                |> Units.timeUnit
                |> Option.bind (fun u ->
                    v
                    |> BigRational.fromFloat
                    |> Option.bind (fun br ->
                        br
                        |> ValueUnit.singleWithUnit u
                        |> ValueUnit.convertTo Units.day
                        |> ValueUnit.getValue
                        |> function
                            | [| days |] ->
                                days
                                |> BigRational.toFloat
                                |> Some
                            | _ -> None
                    )
                )
            target
            |> getAge
            |> function
                | Some min, Some max ->
                    toDays min.Quantity min.Unit,
                    toDays max.Quantity max.Unit
                | Some min, None ->
                    toDays min.Quantity min.Unit,
                    None
                | None, Some max ->
                    None,
                    toDays max.Quantity max.Unit
                | None, None -> None, None


        let getGestAge target =
            match target |> getTarget with
            | Some (_, age, _) ->
                match age with
                | GestationalAge (min, max) -> min, max
                | _ -> None, None
            | _ -> None, None


        let getGestAgeInDays target =
            let toDays v u =
                u
                |> Units.timeUnit
                |> Option.bind (fun u ->
                    v
                    |> BigRational.fromFloat
                    |> Option.bind (fun br ->
                        br
                        |> ValueUnit.singleWithUnit u
                        |> ValueUnit.convertTo Units.day
                        |> ValueUnit.getValue
                        |> function
                            | [| days |] ->
                                days
                                |> BigRational.toFloat
                                |> Some
                            | _ -> None
                    )
                )
            target
            |> getGestAge
            |> function
                | Some min, Some max ->
                    toDays min.Quantity min.Unit,
                    toDays max.Quantity max.Unit
                | Some min, None ->
                    toDays min.Quantity min.Unit,
                    None
                | None, Some max ->
                    None,
                    toDays max.Quantity max.Unit
                | None, None -> None, None


        let getPMAge target =
            match target |> getTarget with
            | Some (_, age, _) ->
                match age with
                | PostMenstrualAge (min, max) -> min, max
                | _ -> None, None
            | _ -> None, None


        let getPMAgeInDays target =
            let toDays v u =
                u
                |> Units.timeUnit
                |> Option.bind (fun u ->
                    v
                    |> BigRational.fromFloat
                    |> Option.bind (fun br ->
                        br
                        |> ValueUnit.singleWithUnit u
                        |> ValueUnit.convertTo Units.day
                        |> ValueUnit.getValue
                        |> function
                            | [| days |] ->
                                days
                                |> BigRational.toFloat
                                |> Some
                            | _ -> None
                    )
                )
            target
            |> getPMAge
            |> function
                | Some min, Some max ->
                    toDays min.Quantity min.Unit,
                    toDays max.Quantity max.Unit
                | Some min, None ->
                    toDays min.Quantity min.Unit,
                    None
                | None, Some max ->
                    None,
                    toDays max.Quantity max.Unit
                | None, None -> None, None


        let getWeight target =
            match target |> getTarget with
            | Some (_, _, weight) ->
                match weight with
                | Weight (min, max) -> min, max
                | _ -> None, None
            | _ -> None, None


        let getWeightInGram target =
            let toGram v u =
                u
                |> Units.weightUnit
                |> Option.bind (fun u ->
                    v
                    |> BigRational.fromFloat
                    |> Option.bind (fun br ->
                        br
                        |> ValueUnit.singleWithUnit u
                        |> ValueUnit.convertTo Units.Weight.gram
                        |> ValueUnit.getValue
                        |> function
                            | [| days |] ->
                                days
                                |> BigRational.toFloat
                                |> Some
                            | _ -> None
                    )
                )
            target
            |> getWeight
            |> function
                | Some min, Some max ->
                    toGram min.Quantity min.Unit,
                    toGram max.Quantity max.Unit
                | Some min, None ->
                    toGram min.Quantity min.Unit,
                    None
                | None, Some max ->
                    None,
                    toGram max.Quantity max.Unit
                | None, None -> None, None


        let genderToString target =
            target
            |> getGender
            |> Option.map (fun tt ->
                match tt with
                | Boy -> "man"
                | Girl -> "vrouw"
                | _ -> ""
            )
            |> Option.defaultValue ""


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




