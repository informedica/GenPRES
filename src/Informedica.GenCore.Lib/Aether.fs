namespace Informedica.GenCore.Lib


module Aether =


    module Morphisms =

        let inline mapToOpt (f1, f2) def =
            (fun x -> x |> Option.map f1 |> Option.defaultValue def),
            (fun x -> x |> f2 |> Some)


        let intYears =
            (fun (y: int<year>) -> y |> int),
            (fun i -> i |> Conversions.yearFromInt)


        let intYearsOpt = mapToOpt intYears


        let intMonths =
            (fun (m: int<month>) -> m |> int),
            (fun i -> i |> Conversions.monthFromInt)


        let intMonthsOpt = mapToOpt intMonths


        let intWeeks =
            (fun (w : int<week>) -> w |> int),
            (fun i -> i |> Conversions.weekFromInt)


        let intWeeksOpt = mapToOpt intWeeks


        let intDays =
            (fun (d : int<day>) -> d |> int),
            (fun i -> i |> Conversions.dayFromInt)


        let intDaysOpt = mapToOpt intDays


