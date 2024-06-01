namespace Informedica.GenOrder.Lib



module Patient =


    open Aether
    open Aether.Operators


    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenUnits.Lib

    type Patient =Types.Patient
    type Access = Locations


    /// <summary>
    /// And empty Patient
    /// </summary>
    let patient : Patient =
        {
            Department = None
            Diagnoses = [||]
            Gender = AnyGender
            Age = None
            Weight = None
            Height = None
            GestAge = None
            PMAge = None
            Locations = []
            RenalFunction = None
        }


    [<AutoOpen>]
    module Optics =


        type Age =
            | Years of int
            | Months of int
            | Weeks of int
            | Days of int


        /// <summary>
        /// Converts a list of Age values to a decimal representing the number of days
        /// </summary>
        /// <param name="ags">The Age values</param>
        let ageToValueUnit ags =
            ags
            |> List.fold (fun acc a ->
                match a with
                | Years x -> (x |> decimal) * 365m
                | Months x -> (x |> decimal) * 30m
                | Weeks x -> (x |> decimal) * 7m
                | Days x -> (x |> decimal)
                |> fun x -> acc + x
            ) 0m
            |> BigRational.fromDecimal
            |> ValueUnit.singleWithUnit Units.Time.day


        /// <summary>
        /// Converts a decimal representing the number of days to a list of Age values
        /// </summary>
        /// <param name="vu">The age ValueUnit</param>
        let ageFromValueUnit (vu : ValueUnit) =
            let vu =
                vu
                |> ValueUnit.convertTo Units.Time.day
                |> ValueUnit.getValue
                |> Array.head
                |> BigRational.toDecimal
            let yrs = (vu / 365m) |> int
            let mos = ((vu - (365 * yrs |> decimal)) / 30m) |> int
            let wks = (vu - (365 * yrs |> decimal) - (30 * mos |> decimal)) / 7m |> int
            let dys = (vu - (365 * yrs |> decimal) - (30 * mos |> decimal) - (7 * wks |> decimal)) |> int
            [
                if yrs > 0 then yrs |> Years
                if mos > 0 then mos |> Months
                if wks > 0 then wks |> Weeks
                if dys > 0 then dys |> Days
            ]

        // Helper method for the Optics below
        let ageAgeList =
            Option.map ageFromValueUnit
            >> (Option.defaultValue []),
            (ageToValueUnit >> Some)


        let age_ = Patient.Age_ >-> ageAgeList


        // Helper method for the Optics below
        let gestPMAgeList =
            let ageFromDec d =
                d
                |> ageFromValueUnit
                |> List.filter (fun a ->
                    match a with
                    | Years _ | Months _ -> false
                    | _ -> true
                )
            Option.map ageFromDec
            >> (Option.defaultValue []),
            (ageToValueUnit >> Some)


        let gestAge_ = Patient.GestAge_ >-> gestPMAgeList

        let pmAge_ = Patient.PMAge_ >-> gestPMAgeList


        type Weight = | Kilogram of decimal | Gram of int


        let vuWeight =
            let get w =
                w
                |> ValueUnit.convertTo Units.Weight.gram
                |> ValueUnit.getValue
                |> Array.head
                |> BigRational.toDecimal
                |> int
                |> Gram

            let set w =
                match w with
                | Kilogram w -> w * 1000m
                | Gram w -> w |> decimal
                |> BigRational.fromDecimal
                |> ValueUnit.singleWithUnit Units.Weight.gram

            Option.map get,
            Option.map set


        let weight_ = Patient.Weight_ >-> vuWeight


        type Height = | Meter of decimal | Centimeter of int


        let vuHeight =
            let get h =
                h
                |> ValueUnit.convertTo Units.Height.centiMeter
                |> ValueUnit.getValue
                |> Array.head
                |> BigRational.toDecimal
                |> int
                |> Centimeter

            let set h =
                match h with
                | Meter h -> h * 100m
                | Centimeter h -> h |> decimal
                |> BigRational.fromDecimal
                |> ValueUnit.singleWithUnit Units.Height.centiMeter

            Option.map get,
            Option.map set


        let height_ = Patient.Height_ >-> vuHeight


    let getGender = Optic.get Patient.Gender_


    let setGender = Optic.set Patient.Gender_


    let getAge = Optic.get age_


    let setAge = Optic.set age_


    let getWeight = Optic.get weight_


    let setWeight = Optic.set weight_


    let getHeight = Optic.get height_


    let setHeight = Optic.set height_


    let getGestAge = Optic.get gestAge_


    let setGestAge = Optic.set gestAge_


    let getPMAge = Optic.get pmAge_


    let setPMAge = Optic.set pmAge_


    let getDepartment = Optic.get Patient.Department_


    let setDepartment = Optic.set Patient.Department_



    let premature =
        patient
        |> setAge [ 1 |> Weeks]
        |> setGestAge [ 32 |> Weeks ]
        |> setWeight (1200 |> Gram |> Some)
        |> setHeight (45 |> Centimeter |> Some)
        |> setDepartment (Some "NEO")


    let newBorn =
        patient
        |> setAge [ 1 |> Weeks]
        |> setWeight (3.5m |> Kilogram |> Some)
        |> setHeight (60 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


    let infant =
        patient
        |> setAge [ 1 |> Years]
        |> setWeight (11.5m |> Kilogram |> Some)
        |> setHeight (70 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


    let toddler =
        patient
        |> setAge [ 3 |> Years]
        |> setWeight (15m |> Kilogram |> Some)
        |> setHeight (90 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


    let child =
        patient
        |> setAge [ 4 |> Years]
        |> setWeight (17m |> Kilogram |> Some)
        |> setHeight (100 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")
        |> fun p -> { p with Locations = [CVL]}


    let teenager =
        patient
        |> setAge [ 12 |> Years]
        |> setWeight (40m |> Kilogram |> Some)
        |> setHeight (150 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


    let adult =
        patient
        |> setAge [ 18 |> Years]
        |> setWeight (70m |> Kilogram |> Some)
        |> setHeight (180 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


