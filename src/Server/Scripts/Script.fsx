// First load all the required libs
// i.e. Giraffe and Saturn being the top level libs
#r "netstandard"
#load "./../../../.paket/load/net472/Server/server.group.fsx"

module Init =
    open System
    open System.IO

    // Set the current directory to the source code directory
    // so, one level up from the scripts dir
    Environment.CurrentDirectory <- Path.Combine(__SOURCE_DIRECTORY__, "./../")

// ==== CODE SECTION ====
module Utils =
    open System

    module String =
        /// Apply `f` to string `s`
        let apply f (s : string) = f s

        /// Utility to enable type inference
        let get = apply id

        /// Count the number of times that a
        /// string t starts with character c
        let countFirstChar c t =
            let _, count =
                if String.IsNullOrEmpty(t) then (false, 0)
                else
                    t
                    |> Seq.fold (fun (flag, dec) c' ->
                           if c' = c && flag then (true, dec + 1)
                           else (false, dec)) (true, 0)
            count

        /// Check if string `s2` contains string `s1`
        let contains =
            fun (s1 : string) (s2 : string) -> (s2 |> get).Contains(s1)

        let toLower s = (s |> get).ToLower()
        let replace (s1 : string) (s2 : string) s = (s |> get).Replace(s1, s2)

    module Math =
        let roundBy s n =
            (n / s)
            |> round
            |> double
            |> (fun f -> f * s)

        let roundBy0_5 = roundBy 0.5

        /// Calculates the number of decimal digits that
        /// should be shown according to a precision
        /// number n that specifies the number of non
        /// zero digits in the decimals.
        /// * 66.666 |> getPrecision 1 = 0
        /// * 6.6666 |> getPrecision 1 = 0
        /// * 0.6666 |> getPrecision 1 = 1
        /// * 0.0666 |> getPrecision 1 = 2
        /// * 0.0666 |> getPrecision 0 = 0
        /// * 0.0666 |> getPrecision 1 = 2
        /// * 0.0666 |> getPrecision 2 = 3
        /// * 0.0666 |> getPrecision 3 = 4
        /// * 6.6666 |> getPrecision 0 = 0
        /// * 6.6666 |> getPrecision 1 = 0
        /// * 6.6666 |> getPrecision 2 = 1
        /// * 6.6666 |> getPrecision 3 = 2
        /// etc
        /// If n < 0 then n = 0 is used.
        let getPrecision n f = // ToDo fix infinity case
            let n =
                if n < 0 then 0
                else n
            if f = 0. || n = 0 then n
            else
                let s =
                    (f
                     |> abs
                     |> string)
                        .Split([| '.' |])

                // calculate number of remaining decimal digits (after '.')
                let p =
                    n - (if s.[0] = "0" then 0
                         else s.[0].Length)

                let p =
                    if p < 0 then 0
                    else p

                if (int s.[0]) > 0 then p
                else
                    // calculate the the first occurance of a non-zero decimal digit
                    let c = (s.[1] |> String.countFirstChar '0')
                    c + p

        /// Fix the precision of a float f to
        /// match a minimum of non zero digits n
        /// * 66.666 |> fixPrecision 1 = 67
        /// * 6.6666 |> fixPrecision 1 = 7
        /// * 0.6666 |> fixPrecision 1 = 0.7
        /// * 0.0666 |> fixPrecision 1 = 0.07
        /// * 0.0666 |> fixPrecision 0 = 0
        /// * 0.0666 |> fixPrecision 1 = 0.07
        /// * 0.0666 |> fixPrecision 2 = 0.067
        /// * 0.0666 |> fixPrecision 3 = 0.0666
        /// * 6.6666 |> fixPrecision 0 = 7
        /// * 6.6666 |> fixPrecision 1 = 7
        /// * 6.6666 |> fixPrecision 2 = 6.7
        /// * 6.6666 |> fixPrecision 3 = 6.67
        /// etc
        /// If n < 0 then n = 0 is used.
        let fixPrecision n (f : float) = Math.Round(f, f |> getPrecision n)

    module List =
        let create x = x :: []

        let findNearestMax n ns =
            match ns with
            | [] -> n
            | _ ->
                ns
                |> List.sort
                |> List.rev
                |> List.fold (fun x a ->
                       if (a - x) < (n - x) then x
                       else a) n

        let removeDuplicates xs =
            xs
            |> List.fold (fun xs x ->
                   if xs |> List.exists ((=) x) then xs
                   else [ x ] |> List.append xs) []

    module DateTime =
        let apply f (dt : DateTime) = f dt
        let get = apply id

        let optionToDate yr mo dy =
            match yr, mo, dy with
            | Some y, Some m, Some d -> new DateTime(y, m, d) |> Some
            | _ -> None

        let dateDiff dt1 dt2 = (dt1 |> get) - (dt2 |> get)
        let dateDiffDays dt1 dt2 = (dateDiff dt1 dt2).Days

        let dateDiffMonths dt1 dt2 =
            (dateDiffDays dt1 dt2)
            |> float
            |> (fun x -> x / 365.)
            |> ((*) 12.)

        let dateDiffYearsMonths dt1 dt2 =
            let mos = (dateDiffMonths dt1 dt2) |> int
            (mos / 12), (mos % 12)

module Data =
    module NormalValues =
        let ageWeight =
            [ (0., 3.785)
              (0.7, 3.785)
              (1.4, 4.415)
              (2.1, 5.)
              (2.8, 5.5)
              (4.2, 6.4)
              (6., 7.4)
              (7.4, 8.)
              (9.2, 8.8)
              (11.1, 9.5)
              (12., 9.8)
              (18., 11.5)
              (24., 12.8)
              (30., 14.1)
              (36., 15.2)
              (42., 16.3)
              (48., 17.4)
              (54., 18.5)
              (60., 19.5)
              (66., 20.5)
              (72., 21.6)
              (78., 22.8)
              (84., 24.2)
              (90., 25.5)
              (96., 26.9)
              (102., 28.3)
              (108., 29.8)
              (114., 31.4)
              (120., 33.1)
              (126., 34.9)
              (132., 36.8)
              (138., 38.9)
              (144., 41.2)
              (150., 43.7)
              (156., 46.2)
              (162., 48.9)
              (168., 51.5)
              (174., 54.1)
              (180., 56.4)
              (186., 58.2)
              (192., 59.8)
              (198., 61.1)
              (204., 62.2)
              (210., 63.3)
              (216., 64.2)
              (222., 65.)
              (228., 65.7)
              (234., 66.1) ]

        let ageHeight =
            [ 0.6792, 52.95
              3., 52.95
              6., 60.5
              9., 67.25
              12., 71.8
              18., 75.6
              24., 82.3
              30., 88.15
              36., 93.1
              42., 97.6
              48., 101.55
              54., 105.25
              60., 108.8
              66., 112.15
              72., 115.35
              78., 118.55
              84., 121.7
              90., 124.7
              96., 127.7
              102., 130.7
              108., 133.5
              114., 136.2
              120., 139.
              126., 141.8
              132., 144.5
              138., 147.25
              144., 150.15
              150., 153.25
              156., 156.25
              162., 159.05
              168., 161.95
              174., 164.9
              180., 167.45
              186., 169.8
              192., 171.5
              198., 172.55
              204., 173.25
              210., 173.8
              216., 174.2
              222., 174.55
              228., 174.85
              234., 175.05
              240., 175.15 ]

        let heartRate =
            [ 1., "90-205"
              12., "90-190"
              36., "80-140"
              72., "65-120"
              144., "58-118"
              180., "50-100"
              228., "50-100" ]

        let respRate =
            [ 12., "30-53"
              36., "22-37"
              72., "20-28"
              144., "18-25"
              228., "12-20" ]

        let sbp =
            [ 1., "60-84"
              12., "72-104"
              36., "86-106"
              72., "89-112"
              120., "97-115"
              144., "102-120"
              228., "110-131" ]

        let dbp =
            [ 1., "31-53"
              12., "37-56"
              36., "42-63"
              72., "46-72"
              120., "57-76"
              144., "61-80"
              228., "64-83" ]

        let gcs =
            [ // Pedicatric
              5 * 12,
              [ "Eye Opening",
                [ (4, "spontaan")
                  (3, "bij geluid")
                  (2, "bij pijn")
                  (1, "geen")
                  (0, "zwelling of verband") ]
                "Motor Response",
                [ (6, "normale spontane bewegingen")
                  (5, "lokaliseert bij pijn")
                  (4, "trekt terug bij pijn")
                  (3, "flexie bij pijn")
                  (2, "extensie bij pijn")
                  (1, "geen reactie op pijn") ]
                "Verbal Response",
                [ (5, "alert, brabbelt, conform leeftijd")
                  (4, "minder, geirriteerd huilen")
                  (3, "huilt bij pijn")
                  (2, "kreunt bij pijn")
                  (1, "geen reactie op pijn")
                  (0, "tube") ] ]
              // Adult
              19 * 12,
              [ "Eye Opening",
                [ (4, "spontaan")
                  (3, "bij geluid")
                  (2, "bij pijn")
                  (1, "geen")
                  (0, "zwelling of verband") ]
                "Motor Response",
                [ (6, "normale spontane bewegingen")
                  (5, "lokaliseert bij pijn")
                  (4, "trekt terug bij pijn")
                  (3, "flexie bij pijn")
                  (2, "extensie bij pijn")
                  (1, "geen reactie op pijn") ]
                "Verbal Response",
                [ (5, "voert opdrachten uit")
                  (4, "verward")
                  (3, "niet adequaat")
                  (2, "niet verstaanbaar")
                  (1, "geen reactie op pijn")
                  (0, "tube") ] ] ]

        let pews =
            [ // Age 0 to 3 mo
              3,
              [ "Ademfrequentie",
                [ (4, "< 15 /min")
                  (2, "15-19 /min")
                  (1, "20-29 /min")
                  (0, "30-60 /min")
                  (1, "61-80 /min")
                  (2, "81-90 /min")
                  (4, "> 90 /min") ]
                "Ademarbeid",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "normaal")
                  (1, "mild verhoogd")
                  (2, "matig verhoogd")
                  (4, "ernstig verhoogd") ]
                "Saturatie",
                [ (4, "")
                  (2, "< 91 %")
                  (1, "91-94 %")
                  (0, "> 94%")
                  (1, "")
                  (2, "")
                  (4, "") ]
                "Zuurstof",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "kamerlucht")
                  (1, "")
                  (2, "extra O2")
                  (4, "O2 via NRB of Optiflow") ]
                "Hartfrequentie",
                [ (4, "< 80 /min")
                  (2, "80-89 /min")
                  (1, "90-109 /min")
                  (0, "110-150 /min")
                  (1, "151-180 /min")
                  (2, "181-190 /min")
                  (4, "> 190 /min") ]
                "Capillaire refill",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "< 3 sec")
                  (1, "")
                  (2, "")
                  (4, "> 3 sec") ]
                "RR (systole)",
                [ (4, "< 45 mmHg")
                  (2, "45-49 mmHg")
                  (1, "50-59 mmHg")
                  (0, "60-80 mmHg")
                  (1, "81-100 mmHg")
                  (2, "101-130 mmHg")
                  (4, "> 130 mmHg") ]
                "Temperatuur",
                [ (4, "")
                  (2, "< 36.0 C")
                  (1, "36.0-36.4 C")
                  (0, "36.5-37.5 C")
                  (1, "37.6-38.5 C")
                  (2, "> 38.5 C")
                  (4, "") ] ]
              // Age 3 to 1 year
              12,
              [ "Ademfrequentie",
                [ (4, "< 15 /min")
                  (2, "15-19 /min")
                  (1, "20-24 /min")
                  (0, "25-50 /min")
                  (1, "51-70 /min")
                  (2, "71-80 /min")
                  (4, "> 80 /min") ]
                "Ademarbeid",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "normaal")
                  (1, "mild verhoogd")
                  (2, "matig verhoogd")
                  (4, "ernstig verhoogd") ]
                "Saturatie",
                [ (4, "")
                  (2, "< 91 %")
                  (1, "91-94 %")
                  (0, "> 94%")
                  (1, "")
                  (2, "")
                  (4, "") ]
                "Zuurstof",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "kamerlucht")
                  (1, "")
                  (2, "extra O2")
                  (4, "O2 via NRB of Optiflow") ]
                "Hartfrequentie",
                [ (4, "< 70 /min")
                  (2, "70-79 /min")
                  (1, "80-99 /min")
                  (0, "100-150 /min")
                  (1, "151-170 /min")
                  (2, "171-180 /min")
                  (4, "> 180 /min") ]
                "Capillaire refill",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "< 3 sec")
                  (1, "")
                  (2, "")
                  (4, "> 3 sec") ]
                "RR (systole)",
                [ (4, "< 60 mmHg")
                  (2, "60-69 mmHg")
                  (1, "70-79 mmHg")
                  (0, "80-100 mmHg")
                  (1, "101-120 mmHg")
                  (2, "121-150 mmHg")
                  (4, "> 150 mmHg") ]
                "Temperatuur",
                [ (4, "")
                  (2, "< 36.0 C")
                  (1, "36.0-36.4 C")
                  (0, "36.5-37.5 C")
                  (1, "37.6-38.5 C")
                  (2, "> 38.5 C")
                  (4, "") ] ]
              // Age 1 to 4 year
              4 * 12,
              [ "Ademfrequentie",
                [ (4, "< 12 /min")
                  (2, "12-14 /min")
                  (1, "15-19 /min")
                  (0, "20-40 /min")
                  (1, "41-60 /min")
                  (2, "61-70 /min")
                  (4, "> 70 /min") ]
                "Ademarbeid",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "normaal")
                  (1, "mild verhoogd")
                  (2, "matig verhoogd")
                  (4, "ernstig verhoogd") ]
                "Saturatie",
                [ (4, "")
                  (2, "< 91 %")
                  (1, "91-94 %")
                  (0, "> 94%")
                  (1, "")
                  (2, "")
                  (4, "") ]
                "Zuurstof",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "kamerlucht")
                  (1, "")
                  (2, "extra O2")
                  (4, "O2 via NRB of Optiflow") ]
                "Hartfrequentie",
                [ (4, "< 60 /min")
                  (2, "60-69 /min")
                  (1, "70-89 /min")
                  (0, "90-120 /min")
                  (1, "121-150 /min")
                  (2, "151-170 /min")
                  (4, "> 170 /min") ]
                "Capillaire refill",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "< 3 sec")
                  (1, "")
                  (2, "")
                  (4, "> 3 sec") ]
                "RR (systole)",
                [ (4, "< 65 mmHg")
                  (2, "65-74 mmHg")
                  (1, "75-89 mmHg")
                  (0, "90-110 mmHg")
                  (1, "111-125 mmHg")
                  (2, "126-160 mmHg")
                  (4, "> 160 mmHg") ]
                "Temperatuur",
                [ (4, "")
                  (2, "< 36.0 C")
                  (1, "36.0-36.4 C")
                  (0, "36.5-37.5 C")
                  (1, "37.6-38.5 C")
                  (2, "> 38.5 C")
                  (4, "") ] ]
              // Age 4 to 12 year
              12 * 12,
              [ "Ademfrequentie",
                [ (4, "< 11 /min")
                  (2, "11-14 /min")
                  (1, "15-19 /min")
                  (0, "20-30 /min")
                  (1, "31-40 /min")
                  (2, "41-50 /min")
                  (4, "> 50 /min") ]
                "Ademarbeid",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "normaal")
                  (1, "mild verhoogd")
                  (2, "matig verhoogd")
                  (4, "ernstig verhoogd") ]
                "Saturatie",
                [ (4, "")
                  (2, "< 91 %")
                  (1, "91-94 %")
                  (0, "> 94%")
                  (1, "")
                  (2, "")
                  (4, "") ]
                "Zuurstof",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "kamerlucht")
                  (1, "")
                  (2, "extra O2")
                  (4, "O2 via NRB of Optiflow") ]
                "Hartfrequentie",
                [ (4, "< 50 /min")
                  (2, "50-59 /min")
                  (1, "60-69 /min")
                  (0, "70-110 /min")
                  (1, "111-130 /min")
                  (2, "131-150 /min")
                  (4, "> 150 /min") ]
                "Capillaire refill",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "< 3 sec")
                  (1, "")
                  (2, "")
                  (4, "> 3 sec") ]
                "RR (systole)",
                [ (4, "< 70 mmHg")
                  (2, "70-79 mmHg")
                  (1, "80-89 mmHg")
                  (0, "90-120 mmHg")
                  (1, "121-140 mmHg")
                  (2, "141-170 mmHg")
                  (4, "> 170 mmHg") ]
                "Temperatuur",
                [ (4, "")
                  (2, "< 36.0 C")
                  (1, "36.0-36.4 C")
                  (0, "36.5-37.5 C")
                  (1, "37.6-38.5 C")
                  (2, "> 38.5 C")
                  (4, "") ] ]
              // Age 12 to 19 year
              19 * 12,
              [ "Ademfrequentie",
                [ (4, "< 10 /min")
                  (2, "10 /min")
                  (1, "11 /min")
                  (0, "12-16 /min")
                  (1, "17-22 /min")
                  (2, "23-30 /min")
                  (4, "> 30 /min") ]
                "Ademarbeid",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "normaal")
                  (1, "mild verhoogd")
                  (2, "matig verhoogd")
                  (4, "ernstig verhoogd") ]
                "Saturatie",
                [ (4, "")
                  (2, "< 91 %")
                  (1, "91-94 %")
                  (0, "> 94%")
                  (1, "")
                  (2, "")
                  (4, "") ]
                "Zuurstof",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "kamerlucht")
                  (1, "")
                  (2, "extra O2")
                  (4, "O2 via NRB of Optiflow") ]
                "Hartfrequentie",
                [ (4, "< 40 /min")
                  (2, "40-49 /min")
                  (1, "50-59 /min")
                  (0, "60-100 /min")
                  (1, "101-120 /min")
                  (2, "121-140 /min")
                  (4, "> 140 /min") ]
                "Capillaire refill",
                [ (4, "")
                  (2, "")
                  (1, "")
                  (0, "< 3 sec")
                  (1, "")
                  (2, "")
                  (4, "> 3 sec") ]
                "RR (systole)",
                [ (4, "< 75 mmHg")
                  (2, "75-84 mmHg")
                  (1, "85-99 mmHg")
                  (0, "100-130 mmHg")
                  (1, "131-150 mmHg")
                  (2, "151-190 mmHg")
                  (4, "> 190 mmHg") ]
                "Temperatuur",
                [ (4, "")
                  (2, "< 36.0 C")
                  (1, "36.0-36.4 C")
                  (0, "36.5-37.5 C")
                  (1, "37.6-38.5 C")
                  (2, "> 38.5 C")
                  (4, "") ] ] ]

    module TreatmentData =
        let joules = [ 1; 2; 3; 5; 7; 10; 20; 30; 50; 70; 100; 150 ]

        // ind, item, dose, min, max, conc, unit, rem
        let medicationDefs =
            [ ("reanimatie", "glucose 10%", 0.2, 0., 25., 0.1, "gram", "")
              ("reanimatie", "NaBic 8,4", 0.5, 0., 50., 1., "mmol", "")
              ("intubatie", "propofol 1%", 2., 0., 0., 10., "mg", "")
              ("intubatie", "propofol 2%", 2., 0., 0., 20., "mg", "")
              ("intubatie", "midazolam", 0.2, 0., 10., 5., "mg", "")
              ("intubatie", "esketamine", 0.5, 0., 5., 5., "mg", "")
              ("intubatie", "etomidaat", 0.5, 0., 20., 2., "mg", "")
              ("intubatie", "fentanyl", 1., 0., 50., 50., "mcg", "")
              ("intubatie", "morfine", 0.1, 0., 10., 1., "mg", "")
              ("intubatie", "rocuronium", 1., 0., 10., 10., "mg", "")
              ("intubatie", "atropine", 0.02, 0.1, 0.5, 0.5, "mg", "")
              ("stridor", "dexamethason", 0.15, 0., 14., 4., "mg", "")

              ("stridor", "adrenaline vernevelen", 5., 5., 5., 1., "mg",
               "5 mg/keer")
              ("astma", "prednisolon", 2., 0., 25., 12.5, "mg", "")
              ("astma", "magnesiumsulfaat 16%", 40., 0., 2000., 160., "mg", "")
              ("astma", "magnesiumsulfaat 50%", 40., 0., 2000., 500., "mg", "")
              ("astma", "salbutamol oplaad", 15., 0., 0., 500., "microg", "")
              ("anafylaxie", "adrenaline im", 0.01, 0., 0.5, 1., "mg", "")

              ("anafylaxie", "adrenaline vernevelen", 5., 5., 5., 1., "mg",
               "5 mg/keer")
              ("anafylaxie", "clemastine", 0.05, 0., 2., 1., "mg", "")
              ("anafylaxie", "hydrocortison", 4., 0., 100., 10., "mg", "")

              ("anti-arrythmica", "adenosine 1e gift", 100., 0., 12000., 3000.,
               "microg", "")

              ("anti-arrythmica", "adenosine 2e gift", 200., 0., 12000., 3000.,
               "microg", "")

              ("anti-arrythmica", "adenosine 3e gift", 300., 0., 12000., 3000.,
               "microg", "")
              ("anti-arrythmica", "amiodarone", 5., 0., 300., 50., "mg", "")
              ("antidota", "flumazine", 0.02, 0., 0.3, 0.1, "mg", "")
              ("antidota", "naloxon", 0.01, 0., 0.5, 0.02, "mg", "")
              ("antidota", "naloxon", 0.01, 0., 0.5, 0.02, "mg", "")
              ("antidota", "intralipid 20%", 1.5, 0., 0., 1., "ml", "")
              ("antidota", "fenytoine", 5., 0., 1500., 50., "mg", "")
              ("hyperkaliemie", "resonium klysma", 1., 0., 0., 0.15, "gram", "")
              ("hyperkaliemie", "furosemide", 1., 0., 0., 10., "mg", "")
              ("hyperkaliemie", "NaBic 8,4%", 2.5, 0., 0., 1., "mmol", "")

              ("hyperkaliemie", "calciumgluconaat", 0.13, 0., 4.5, 0.225, "mg",
               "")
              ("anticonvulsiva", "diazepam rect", 0.5, 0., 0., 4., "mg", "")
              ("anticonvulsiva", "diazepam iv", 0.25, 0., 0., 5., "mg", "")
              ("anticonvulsiva", "fenytoine", 20., 0., 1500., 50., "mg", "")
              ("anticonvulsiva", "midazolam", 0.1, 0., 10., 5., "mg", "")

              ("anticonvulsiva", "midazolam buc/nas/im", 0.2, 0., 10., 5., "mg",
               "")
              ("anticonvulsiva", "thiopental", 5., 0., 0., 25., "mg", "")
              ("hersenoedeem", "mannitol 15%", 0.5, 0., 50., 0.15, "gram", "")
              ("hersenoedeem", "NaCl 2,9%", 3., 0., 0., 1., "ml", "")
              ("pijnstilling", "paracetamol", 20., 0., 100., 10., "mg", "")
              ("pijnstilling", "diclofenac", 1., 0., 75., 25., "mg", "")
              ("pijnstilling", "fentanyl", 1., 0., 0., 50., "microg", "")
              ("pijnstilling", "morfine", 0.1, 0., 0., 1., "mg", "")
              ("anti-emetica", "dexamethason", 0.1, 0., 8., 4., "mg", "")
              ("anti-emetica", "ondansetron", 0.1, 0., 4., 2., "mg", "")
              ("anti-emetica", "droperidol", 0.02, 0., 1.25, 2.5, "mg", "")
              ("anti-emetica", "metoclopramide", 0.1, 0., 10., 5., "mg", "")

              ("elektrolyten", "kaliumchloride 7,4%", 0.5, 0., 40., 1.0, "mmol",
               "")

              ("elektrolyten", "calciumgluconaat", 0.13, 0., 4.5, 0.225, "mmol",
               "")

              ("elektrolyten", "magnesiumchloride 10%", 0.08, 0., 0., 0.5,
               "mmol", "")
              ("lokaal anesthesie", "licocaine 1%", 5., 0., 200., 10., "mg", "")
              ("lokaal anesthesie", "licocaine 2%", 5., 0., 200., 20., "mg", "")
              ("lokaal anesthesie", "bupivacaine", 3., 0., 0., 2.5, "mg", "")

              ("lokaal anesthesie", "bupivacaine/adrenaline", 3., 0., 0., 2.5,
               "mg", "") ]

        //                                          Standaard oplossingen								            Advies doseringen
        //                                          2 tot 6		    6 tot 11	    11 tot 40	    vanaf 40
        // Tbl_Ped_MedContIV	Eenheid	DosEenheid	Hoev	Vol	    Hoev	Vol	    Hoev	Vol	    Hoev	Vol	    MinDos	MaxDos	AbsMax	MinConc	MaxConc	OplKeuze
        let contMeds =
            [ "inotropie", "adrenaline", "mg", "microg/kg/min", 1., 50., 2., 50.,
              5., 50., 5., 50., 0.05, 0.5, 1., 0., 0.1, "NaCl of Gluc"

              "suppletie", "albumine 20%", "g", "gram/kg/dag", 0.2, 1., 0.2, 1.,
              0.2, 1., 0.2, 1., 1., 2., 4., 0., 0., ""

              "ductus afhankelijk cor vitium", "alprostadil", "mg",
              "nanog/kg/min", 0.2, 50., 0., 0., 0., 0., 0., 0., 10., 50., 100.,
              0., 0.02, "NaCl of Gluc"

              "anti-arrythmica", "amiodarone", "mg", "microg/kg/min", 50., 50.,
              150., 50., 300., 50., 600., 50., 5., 15., 25., 0.6, 50., "Gluc 5%"

              "sedatie", "clonidine", "mg", "microg/kg/uur", 0.15, 50., 0.3, 50.,
              0.6, 50., 0.6, 50., 0.25, 2., 3., 0., 0.15, "NaCl"

              "inotropie", "dobutamine", "mg", "microg/kg/min", 80., 50., 200.,
              50., 400., 50., 400., 50., 1., 20., 30., 0., 12.5, "NaCl of Gluc"

              "inotropie", "dopamine", "mg", "microg/kg/min", 80., 50., 200.,
              50., 400., 50., 400., 50., 1., 20., 30., 0., 40., "NaCl of Gluc"

              // "pijnstilling", "Epi bupi 1,25mg /ml", "ml", "ml/uur", 0., 24., 0., 48., 0., 48., 0., 48., 1., 8., 8., 0., 0., ""
              // "pijnstilling", "Epi bupi 1,25mg, sufenta 0,5 mcg /ml", "ml", "ml/uur", 0., 24., 0., 48., 0., 48., 0., 48., 1., 8., 8., 0., 0., ""
              "pulmonale hypertensie", "epoprostenol", "mg", "nanog/kg/min", 0.2,
              50., 0.4, 50., 0.8, 50., 0.8, 50., 0.5, 50., 50., 0.005, 0.01,
              "glycine buffer"

              "sedatie", "esketamine", "mg", "mg/kg/uur", 100., 50., 250., 50.,
              250., 50., 250., 50., 0.5, 1., 2., 0., 5., "NaCl of Gluc"

              "antihypertensiva", "esmolol", "mg", "mg/kg/min", 500., 50., 500.,
              50., 500., 50., 500., 50., 0.1, 1., 2., 0., 10., "NaCl of Gluc"

              "pijnstilling", "fentanyl", "mg", "microg/kg/uur", 0.5, 50., 1.,
              50., 2.5, 50., 2.5, 50., 1., 5., 10., 0., 0.05, "NaCl of Gluc"

              "vasopressie", "fenylefrine", "mg", "microg/kg/min", 1.5, 50., 2.5,
              50., 5., 50., 5., 50., 0.05, 5., 10., 0., 10., "NaCl of Gluc"

              "diuretica", "furosemide", "mg", "mg/kg/dag", 10., 50., 20., 50.,
              40., 50., 100., 50., 1., 4., 6., 0., 10., "NaCl"

              "antistolling", "heparine", "IE", "IE/kg/uur", 5000., 50., 10000.,
              50., 20000., 50., 20000., 50., 10., 20., 50., 0., 1000., "NaCl"

              "glucose regulatie", "insuline", "IE", "IE/kg/uur", 10., 50., 10.,
              50., 50., 50., 50., 50., 0.02, 0.125, 2., 0., 1., "NaCl"

              "chronotropie", "isoprenaline", "mg", "microg/kg/min", 2., 50., 2.,
              50., 2., 50., 2., 50., 0.01, 1.5, 3., 0., 1., "Gluc"

              "antihypertensiva", "labetalol", "mg", "mg/kg/uur", 250., 50.,
              250., 50., 250., 50., 250., 50., 0.25, 3., 4., 0., 5.,
              "NaCl of Gluc"

              "bronchodilatie", "magnesiumsulfaat", "mg", "mg/kg/uur", 500., 50.,
              1000., 50., 2000., 50., 2000., 50., 3., 20., 25., 1., 160.,
              "NaCl of Gluc"

              "sedatie", "midazolam", "mg", "mg/kg/uur", 25., 50., 50., 50., 50.,
              50., 100., 50., 0.05, 0.5, 1., 0., 5., "NaCl of Gluc"

              "inotropie", "milrinone", "mg", "microg/kg/min", 5., 50., 10., 50.,
              20., 50., 20., 50., 0.15, 0.5, 0.75, 0., 1., "NaCl of Gluc"

              "pijnstilling", "morfine", "mg", "mg/kg/dag", 2., 50., 5., 50.,
              10., 50., 50., 50., 0.1, 0.5, 1., 0., 1., "NaCl of Gluc"

              "suppletie", "NaCl 2,9%", "mmol", "mmol/kg/dag", 25., 50., 25.,
              50., 25., 50., 25., 50., 2., 4., 6., 0., 0., ""

              "antihypertensiva", "nitroprusside", "mg", "microg/kg/min", 10.,
              50., 20., 50., 40., 50., 40., 50., 0.5, 8., 10., 0., 10.,
              "NaCl of Gluc"

              "vasopressie", "noradrenaline", "mg", "microg/kg/min", 1., 50., 2.,
              50., 5., 50., 5., 50., 0.05, 0.5, 1., 0., 1., "NaCl of Gluc"

              "sedatie", "propofol 1%", "mg", "mg/kg/uur", 10., 1., 10., 1., 10.,
              1., 10., 1., 1., 4., 4., 0., 0., ""

              "sedatie", "propofol 2%", "mg", "mg/kg/uur", 20., 1., 20., 1., 20.,
              1., 20., 1., 1., 4., 4., 0., 0., ""

              "verslapping", "rocuronium", "mg", "mg/kg/uur", 50., 50., 100.,
              50., 200., 50., 200., 50., 0.6, 1.2, 2., 0., 10., "NaCl of Gluc"

              "bronchodilatie", "salbutamol", "mg", "microg/kg/min", 5., 50.,
              10., 50., 20., 50., 20., 50., 0.1, 10., 15., 0.005, 0.42,
              "NaCl of Gluc"

              "sedatie", "thiopental", "mg", "mg/kg/uur", 1250., 50., 1250., 50.,
              1250., 50., 1250., 50., 5., 10., 20., 5., 25., "NaCl of Gluc" ]

        let products =
            [ "inotropie", "adrenaline", "mg", 1.
              "suppletie", "albumine 20%", "g", 0.
              "ductus afhankelijk cor vitium", "alprostadil", "mg", 0.5
              "anti-arrythmica", "amiodarone", "mg", 50.
              "sedatie", "clonidine", "mg", 0.15
              "inotropie", "dobutamine", "mg", 12.5
              "inotropie", "dopamine", "mg", 40.
              // "pijnstilling", "Epi bupi 1,25mg /ml", "ml", "ml/uur", 0., 24., 0., 48., 0., 48., 0., 48., 1., 8., 8., 0., 0., ""
              // "pijnstilling", "Epi bupi 1,25mg, sufenta 0,5 mcg /ml", "ml", "ml/uur", 0., 24., 0., 48., 0., 48., 0., 48., 1., 8., 8., 0., 0., ""
              "pulmonale hypertensie", "epoprostenol", "mg", (50. / 1000.)
              "sedatie", "esketamine", "mg", 5.
              "antihypertensiva", "esmolol", "mg", 10.
              "pijnstilling", "fentanyl", "mg", 0.05
              "vasopressie", "fenylefrine", "mg", 10.
              "diuretica", "furosemide", "mg", 10.
              "antistolling", "heparine", "IE", 5000.
              "glucose regulatie", "insuline", "IE", 100.
              "chronotropie", "isoprenaline", "mg", 1.
              "antihypertensiva", "labetalol", "mg", 5.
              "bronchodilatie", "magnesiumsulfaat", "mg", 100.
              "sedatie", "midazolam", "mg", 5.
              "inotropie", "milrinone", "mg", 1.
              "pijnstilling", "morfine", "mg", 10.
              "suppletie", "NaCl 2,9%", "mmol", 0.5
              "antihypertensiva", "nitroprusside", "mg", 25.
              "vasopressie", "noradrenaline", "mg", 1.
              "sedatie", "propofol 1%", "mg", 0.
              "sedatie", "propofol 2%", "mg", 0.
              "verslapping", "rocuronium", "mg", 10.
              "bronchodilatie", "salbutamol", "mg", 1.
              "sedatie", "thiopental", "mg", 25. ]

open Data


module DataTests =
    open Thoth.Json
    open System.IO

    let path = Path.Combine(System.Environment.CurrentDirectory, "./../../data/data/")
    let encode ob = Thoth.Json.Net.Encode.Auto.toString(0, ob, false)
    let decode<'T> s = Thoth.Json.Net.Decode.Auto.unsafeFromString<'T>(s)

    let writeToFile file ob =
        let file = Path.Combine(path, file)
        let json = encode ob
        printfn "writing to file: %s" file
        File.WriteAllText(file, json)

    let readFromFile<'T> file =
        let file = Path.Combine(path, file)
        File.ReadAllText(file)
        |> decode<'T>

    Data.NormalValues.ageHeight
    |> writeToFile "AgeHeight.json"

    "AgeHeight.json"
    |> readFromFile<((float * float) list)>
    |> printfn "%A"


    Data.NormalValues.ageWeight
    |> writeToFile "AgeWeight.json"

    "AgeWeight.json"
    |> readFromFile<((float * float) list)>
    |> printfn "%A"


    Data.NormalValues.dbp
    |> writeToFile "dbp.json"

    "dbp.json"
    |> readFromFile<((float * string) list)>
    |> printfn "%A"

    Data.NormalValues.sbp
    |> writeToFile "sbp.json"

    "sbp.json"
    |> readFromFile<((float * string) list)>
    |> printfn "%A"

    Data.NormalValues.gcs
    |> writeToFile "gcs.json"

    "gcs.json"
    |> readFromFile<(int * (string * (int * string) list) list) list>

    Data.NormalValues.heartRate
    |> writeToFile "hearRate.json"

    "hearRate.json"
    |> readFromFile<(float * string) list>

    Data.NormalValues.respRate
    |> writeToFile "respRate.json"

    "respRate.json"
    |> readFromFile<(float * string) list>

    Data.NormalValues.pews
    |> writeToFile "pews.json"

    "pews.json"
    |> readFromFile<(int * (string * (int * string) list) list) list>

    Data.TreatmentData.contMeds
    |> writeToFile "contMeds.json"

    "contMeds.json"
    |> readFromFile<(string * string * string * string * float * float * float * float * float * float * float * float * float * float * float * float * float * string) list>


    Data.TreatmentData.medicationDefs
    |> writeToFile "medicationDefs.json"

    "medicationDefs.json"
    |> readFromFile<(string * string * float * float * float * float * string * string) list>

    Data.TreatmentData.joules
    |> writeToFile "joules.json"

    "joules.json"
    |> readFromFile<int list>

    Data.TreatmentData.products
    |> writeToFile "products.json"

    "products.json"
    |> readFromFile<(string * string * string * float) list>

/// This module defines shared types between
/// the client and the server
module Types =
    module Configuration =
        type Configuration = Setting list

        and Setting =
            { Department : string
              MinAge : int
              MaxAge : int
              MinWeight : float
              MaxWeight : float }

    module Patient =
        module Age =
            type Age =
                { Years : int
                  Months : int
                  Weeks : int
                  Days : int }

        type Age = Age.Age

        /// Patient model for calculations
        type Patient =
            { Age : Age
              Weight : Weight
              Height : Height }

        /// Weight in kg
        and Weight =
            { Estimated : double
              Measured : double }

        /// Length in cm
        and Height =
            { Estimated : double
              Measured : double }

    module Request =
        module Configuration =
            type Msg = Get

        module Patient =
            type Msg =
                | Init
                | Clear
                | Get
                | Year of int
                | Month of int
                | Weight of float
                | Height of float

        module AcuteList =
            type Msg = Get of Patient.Patient

        type Msg =
            | PatientMsg of Patient.Msg
            | ConfigMsg of Configuration.Msg

    module Response =
        type Response =
            | Configuration of Configuration.Configuration
            | Patient of Patient.Patient

module Configuration =
    open System.IO
    open Thoth.Json.Net
    open Types.Configuration

    let dataPath = Path.GetFullPath "./../../data/config/genpres.config.json"

    let createSettings dep mina maxa minw maxw =
        { Department = dep
          MinAge = mina
          MaxAge = maxa
          MinWeight = minw
          MaxWeight = maxw }

    let getSettings() =
        Path.GetFullPath dataPath
        |> File.ReadAllText
        |> Decode.Auto.unsafeFromString<Configuration>

module ConfigurationTest =
    Configuration.getSettings() |> printfn "%A"

module Patient =
    open Utils

    module Age =
        open Types.Patient.Age

        let (>==) r f = Result.bind f r

        let ageZero =
            { Years = 0
              Months = 0
              Weeks = 0
              Days = 0 }

        let validateMinMax lbl min max n =
            if n >= min && n <= max then Result.Ok n
            else
                sprintf "%s: %i not >= %i and <= %i" lbl n min max
                |> Result.Error

        let set setter lbl min max n age =
            n
            |> validateMinMax lbl min max
            >== ((setter age) >> Result.Ok)
        let setYears = set (fun age n -> { age with Years = n }) "Years" 0 100
        let setMonths mos age =
            age
            |> setYears (mos / 12)
            >== set (fun age n -> { age with Months = n }) "Months" 0 11
                    (mos % 12)

        let setWeeks wks age =
            let yrs = wks / 52
            let mos = (wks - yrs * 52) / 4
            let wks = wks - (mos * 4) - (yrs * 52)
            age
            |> setYears yrs
            >== set (fun age n -> { age with Months = n }) "Months" 0 12 mos
            >== set (fun age n -> { age with Weeks = n }) "Weeks" 0 4 wks

        let setDays dys age =
            let c = 356. / 12.
            let yrs = dys / 356
            let mos = ((float dys) - (float yrs) * 356.) / c |> int

            let wks =
                (float dys) - ((float mos) * c) - (yrs * 356 |> float)
                |> int
                |> fun x -> x / 7

            let dys =
                (float dys) - ((float mos) * c) - (yrs * 356 |> float)
                |> int
                |> fun x -> x % 7

            age
            |> setYears yrs
            >== set (fun age n -> { age with Months = n }) "Months" 0 12 mos
            >== set (fun age n -> { age with Weeks = n }) "Weeks" 0 4 wks
            >== set (fun age n -> { age with Days = n }) "Days" 0 6 dys

        let getYears { Years = yrs } = yrs
        let getMonths { Months = mos } = mos
        let getWeeks { Weeks = ws } = ws
        let getDays { Days = ds } = ds

        let calcYears a =
            (a
             |> getYears
             |> float)
            + ((a
                |> getMonths
                |> float)
               / 12.)

        let calcMonths a = (a |> getYears) * 12 + (a |> getMonths)

    open Types.Patient

    let apply f (p : Patient) = f p
    let get = apply id

    /// Estimate the weight according to age
    /// in `yr` years and `mo` months
    let ageToWeight yr mo =
        let age = (double yr) * 12. + (double mo)
        match Data.NormalValues.ageWeight
              |> List.filter (fun (a, _) -> age <= a) with
        | (_, w) :: _ -> w
        | [] -> 0.

    let ageToHeight yr mo =
        let age = (double yr) * 12. + (double mo)
        match Data.NormalValues.ageHeight |> List.filter (fun (a, _) -> age < a) with
        | (_, h) :: _ -> h
        | _ -> 0.

    let patient =
        let age = Age.ageZero

        let wght : Weight =
            { Estimated = ageToWeight 0 0
              Measured = 0. }

        let hght =
            { Estimated = ageToHeight 0 0
              Measured = 0. }

        { Age = age
          Weight = wght
          Height = hght }

    let getAge p = (p |> get).Age
    let getAgeYears p = (p |> getAge).Years
    let getAgeMonths p = (p |> getAge).Months

    let getAgeInYears p =
        (p
         |> getAgeYears
         |> float)
        + ((p
            |> getAgeMonths
            |> float)
           / 12.)

    let getAgeInMonths p = (p |> getAgeYears) * 12 + (p |> getAgeMonths)

    /// Get either the measured weight or the
    /// estimated weight if measured weight = 0
    let getWeight pat =
        if (pat |> get).Weight.Measured = 0. then pat.Weight.Estimated
        else pat.Weight.Measured

    /// Get either the measured height or the
    /// estimated height if measured weight = 0
    let getHeight pat =
        if (pat |> get).Height.Measured = 0. then pat.Height.Estimated
        else pat.Height.Measured

    /// ToDo: make function more general by
    /// being able to set mo > 12 -> yr
    let private updateAge yr mo (pat : Patient) =
        match yr, mo with
        | Some y, None ->
            if y > 18 || y < 0 then pat
            else
                let w = ageToWeight y (pat.Age.Months)
                let h = ageToHeight y (pat.Age.Months)
                { pat with Age = { pat.Age with Years = y }
                           Weight = { pat.Weight with Weight.Estimated = w }
                           Height = { pat.Height with Estimated = h } }
        | None, Some m ->
            let age = pat.Age
            let w = ageToWeight (age.Years) m

            let y =
                if m = 12 && age.Years < 18 then age.Years + 1
                else if m = -1 && pat.Age.Years > 0 then age.Years - 1
                else age.Years

            let m =
                if m >= 12 then 0
                else if m = -1 && y = 0 then 0
                else if m = -1 && y > 0 then 11
                else m

            let h = ageToHeight y (pat.Age.Months)
            { pat with Age =
                           { pat.Age with Years = y
                                          Months = m }
                       Weight = { pat.Weight with Weight.Estimated = w }
                       Height = { pat.Height with Estimated = h } }
        | _ -> pat

    let updateAgeYears yr = updateAge (Some yr) None
    let updateAgeMonths mo = updateAge None (Some mo)

    let updateWeightGram gr pat =
        let kg = gr / 1000.
        { (pat |> get) with Weight = { pat.Weight with Measured = kg } }

    let calcBMI isEst pat =
        let l =
            if isEst then pat.Height.Estimated
            else pat |> getHeight
            |> fun x -> x / 100.

        let w =
            if isEst then pat.Weight.Estimated
            else pat |> getWeight

        if l > 0. then (w / (l ** 2.)) |> Some
        else None

    let calcBSA isEst pat =
        let l =
            if isEst then pat.Height.Estimated
            else pat |> getHeight

        let w =
            if isEst then pat.Weight.Estimated
            else pat |> getWeight

        if l > 0. then sqrt (w * ((l |> float)) / 3600.) |> Some
        else None

    let calcNormalFluid pat =
        let a = pat |> getAge
        a

    let show pat =
        let pat = pat |> get

        let wght =
            let w = pat |> getWeight
            if w < 2. then ""
            else
                w
                |> Math.fixPrecision 2
                |> string

        let ew =
            pat.Weight.Estimated
            |> Math.fixPrecision 2
            |> string

        let bsa =
            match pat |> calcBSA false with
            | Some bsa -> sprintf ", BSA %A m2" (bsa |> Math.fixPrecision 2)
            | None -> ""

        sprintf
            "Leeftijd: %i jaren en %i maanden, Gewicht: %s kg (geschat %s kg)%s"
            pat.Age.Years pat.Age.Months wght ew bsa

module App =
    open System.IO
    open Microsoft.Extensions.DependencyInjection
    open FSharp.Control.Tasks.V2
    open Giraffe
    open Saturn
    open Types

    let processRequest (req : Request.Msg) =
        match req with
        | Request.ConfigMsg msg ->
            match msg with
            | Request.Configuration.Get ->
                Configuration.getSettings()
                |> Types.Response.Configuration
                |> Some
        | Request.PatientMsg msg ->
            match msg with
            | Request.Patient.Init ->
                Patient.patient
                |> Response.Patient
                |> Some
            | _ -> None
        | _ -> None

    let tryGetEnv =
        System.Environment.GetEnvironmentVariable
        >> function
        | null
        | "" -> None
        | x -> Some x

    let publicPath = Path.GetFullPath "../Client/public"

    let port =
        "SERVER_PORT"
        |> tryGetEnv
        |> Option.map uint16
        |> Option.defaultValue 8085us

    let webApp =
        router
            {
            post "/api/request"
                (fun next ctx -> task { let! resp = task
                                                        {
                                                        let! req = ctx.BindJsonAsync<Request.Msg>
                                                                       ()
                                                        return req
                                                               |> processRequest }
                                        return! json resp next ctx }) }
    let configureSerialization (services : IServiceCollection) =
        services.AddSingleton<Giraffe.Serialization.Json.IJsonSerializer>
            (Thoth.Json.Giraffe.ThothSerializer())

    let app =
        application {
            url ("http://0.0.0.0:" + port.ToString() + "/")
            use_router webApp
            memory_cache
            use_static publicPath
            service_config configureSerialization
            use_gzip
        }

// ==== TEST SECTION ====
module Application =
    open Microsoft.AspNetCore.Hosting

    ///Runs Saturn application with the ability to stop the server
    let start (app : IWebHostBuilder) token =
        app.Build().RunAsync(token) |> ignore

module TestServer =
    open System
    open System.IO
    open Thoth.Json.Net
    open Saturn
    open System.Threading
    open System.Net

    type WebServerMsg =
        | Start
        | Stop

    let createWebServer() =
        let source = new CancellationTokenSource()
        MailboxProcessor.Start <| fun inbox ->
            let rec loop (source : CancellationTokenSource) =
                async {
                    let! msg = inbox.Receive()
                    match msg with
                    | Start ->
                        printfn "Starting the webserver"
                        Application.start App.app source.Token
                        return! loop source
                    | Stop ->
                        printfn "Stopping the webserver"
                        source.Cancel()
                        return ()
                }
            loop source

    let server = createWebServer()

    type Method =
        | GET
        | POST

    let fetchUrl method json url =
        async {
            let encode json = Encode.Auto.toString (0, json, false)
            let req = WebRequest.Create(Uri(url))
            req.ContentType <- "application/json"
            req.Method <- match method with
                          | GET -> "GET"
                          | POST -> "POST"
            match json with
            | Some json ->
                use streamWriter = new StreamWriter(req.GetRequestStream())
                let s = json |> encode
                streamWriter.Write(s)
                streamWriter.Flush()
                streamWriter.Close()
            | None -> ()
            use resp = req.GetResponse()
            use stream = resp.GetResponseStream()
            use reader = new IO.StreamReader(stream)
            try
                let html = reader.ReadToEnd()
                printfn "finished downloading %s" url
                return html
            with e ->
                printfn "error: %s" e.Message
                return ""
        }

open Thoth.Json.Net
open TestServer
open Types

// Start testing
Start |> server.Post
// Run the tests
// Get settings
"http://localhost:8085/api/request"
|> fetchUrl POST (Request.Configuration.Get
                  |> Request.ConfigMsg
                  |> Some)
|> Async.RunSynchronously
|> Decode.Auto.unsafeFromString<Response.Response Option>
// Get initial paitient
"http://localhost:8085/api/request"
|> fetchUrl POST (Request.Patient.Init
                  |> Request.PatientMsg
                  |> Some)
|> Async.RunSynchronously
|> Decode.Auto.unsafeFromString<Response.Response Option>
// Stop the test server
Stop |> server.Post
