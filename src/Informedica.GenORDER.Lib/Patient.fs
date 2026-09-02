namespace Informedica.GenOrder.Lib


module Patient =


    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenUnits.Lib

    type Patient = Types.Patient
    type Access = AccessDevice


    /// <summary>
    /// And empty Patient
    /// </summary>
    let patient: Patient =
        {
            Location = None
            Department = None
            Diagnoses = [||]
            Gender = AnyGender
            Age = None
            Weight = None
            Height = None
            GestAge = None
            PMAge = None
            Access = []
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
            |> List.fold
                (fun acc a ->
                    match a with
                    | Years x -> (x |> decimal) * 365m
                    | Months x -> (x |> decimal) * 30m
                    | Weeks x -> (x |> decimal) * 7m
                    | Days x -> (x |> decimal)
                    |> fun x -> acc + x
                )
                0m
            |> BigRational.fromDecimal
            |> ValueUnit.singleWithUnit Units.Time.day


        /// <summary>
        /// Converts a decimal representing the number of days to a list of Age values
        /// </summary>
        /// <param name="vu">The age ValueUnit</param>
        let ageFromValueUnit (vu: ValueUnit) =
            let vu =
                vu
                |> ValueUnit.convertTo Units.Time.day
                |> ValueUnit.getValue
                |> Array.head
                |> BigRational.toDecimal

            let yrs = (vu / 365m) |> int
            let mos = ((vu - (365 * yrs |> decimal)) / 30m) |> int
            let wks = (vu - (365 * yrs |> decimal) - (30 * mos |> decimal)) / 7m |> int

            let dys =
                (vu - (365 * yrs |> decimal) - (30 * mos |> decimal) - (7 * wks |> decimal))
                |> int

            [
                if yrs > 0 then
                    yrs |> Years
                if mos > 0 then
                    mos |> Months
                if wks > 0 then
                    wks |> Weeks
                if dys > 0 then
                    dys |> Days
            ]

        // Helper pair (to list of Age / from list of Age) for the get/set below
        let ageAgeList =
            Option.map ageFromValueUnit >> (Option.defaultValue []), (ageToValueUnit >> Some)


        // Helper pair for the get/set below
        let gestPMAgeList =
            let ageFromDec d =
                d
                |> ageFromValueUnit
                |> List.filter (fun a ->
                    match a with
                    | Years _
                    | Months _ -> false
                    | _ -> true
                )

            Option.map ageFromDec >> (Option.defaultValue []), (ageToValueUnit >> Some)


        type Weight =
            | Kilogram of decimal
            | Gram of int


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

            Option.map get, Option.map set


        type Height =
            | Meter of decimal
            | Centimeter of int


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

            Option.map get, Option.map set


    let getGender (p: Patient) = p.Gender


    let setGender g (p: Patient) = { p with Gender = g }


    let getAge (p: Patient) = p.Age |> fst ageAgeList


    let setAge ags (p: Patient) = { p with Age = ags |> snd ageAgeList }


    let getWeight (p: Patient) = p.Weight |> fst vuWeight


    let setWeight w (p: Patient) = { p with Weight = w |> snd vuWeight }


    let getHeight (p: Patient) = p.Height |> fst vuHeight


    let setHeight h (p: Patient) = { p with Height = h |> snd vuHeight }


    let getGestAge (p: Patient) = p.GestAge |> fst gestPMAgeList


    let setGestAge ags (p: Patient) =
        { p with GestAge = ags |> snd gestPMAgeList }


    let getPMAge (p: Patient) = p.PMAge |> fst gestPMAgeList


    let setPMAge ags (p: Patient) =
        { p with PMAge = ags |> snd gestPMAgeList }


    let getDepartment (p: Patient) = p.Department


    let setDepartment d (p: Patient) = { p with Department = d }


    let premature =
        patient
        |> setAge [ 1 |> Weeks ]
        |> setGestAge [ 32 |> Weeks ]
        |> setWeight (1200 |> Gram |> Some)
        |> setHeight (45 |> Centimeter |> Some)
        |> setDepartment (Some "NEO")


    let newBorn =
        patient
        |> setAge [ 1 |> Weeks ]
        |> setWeight (3.5m |> Kilogram |> Some)
        |> setHeight (60 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


    let infant =
        patient
        |> setAge [ 1 |> Years ]
        |> setWeight (11.5m |> Kilogram |> Some)
        |> setHeight (70 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


    let toddler =
        patient
        |> setAge [ 3 |> Years ]
        |> setWeight (15m |> Kilogram |> Some)
        |> setHeight (90 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


    let child =
        patient
        |> setAge [ 4 |> Years ]
        |> setWeight (17m |> Kilogram |> Some)
        |> setHeight (100 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")
        |> fun p -> { p with Access = [ CVL ] }


    let teenager =
        patient
        |> setAge [ 12 |> Years ]
        |> setWeight (40m |> Kilogram |> Some)
        |> setHeight (150 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


    let adult =
        patient
        |> setAge [ 18 |> Years ]
        |> setWeight (70m |> Kilogram |> Some)
        |> setHeight (180 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")
